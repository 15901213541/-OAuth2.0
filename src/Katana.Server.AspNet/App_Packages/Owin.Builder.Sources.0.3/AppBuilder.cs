using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Owin.Builder.Utils;

namespace Owin.Builder
{
    using ResultTuple = Tuple< //Result
        IDictionary<string, object>, // Properties
        int, // Status
        IDictionary<string, string[]>, // Headers
        Func< // CopyTo
            Stream, // Body
            Task>>; // Done

    internal class AppBuilder : IAppBuilder
    {
        public AppBuilder()
        {
            _properties = new Dictionary<string, object>();
            _conversions = new Dictionary<Tuple<Type, Type>, Delegate>();
            _middleware = new List<Tuple<Type, Delegate, object[]>>();
        }

        public AppBuilder(
            IDictionary<Tuple<Type, Type>, Delegate> conversions,
            IDictionary<string, object> properties)
        {
            _properties = properties;
            _conversions = conversions;
            _middleware = new List<Tuple<Type, Delegate, object[]>>();
        }

        readonly IList<Tuple<Type, Delegate, object[]>> _middleware;
        readonly IDictionary<Tuple<Type, Type>, Delegate> _conversions;
        readonly IDictionary<string, object> _properties;

        public IDictionary<string, object> Properties { get { return _properties; } }

        public IAppBuilder Use(object middleware, params object[] args)
        {
            _middleware.Add(ToMiddlewareFactory(middleware, args));
            return this;
        }

        public IAppBuilder New()
        {
            return new AppBuilder(_conversions, _properties);
        }

        public object Build(Type returnType)
        {
            return BuildInternal(returnType);
        }

        public IAppBuilder AddSignatureConversion(Delegate conversion)
        {
            var parameterType = GetParameterType(conversion);
            if (parameterType == null)
            {
                throw new ArgumentException("Conversion delegate must take one parameter", "conversion");
            }
            var key = Tuple.Create(conversion.Method.ReturnType, parameterType);
            _conversions[key] = conversion;
            return this;
        }

        Type GetParameterType(Delegate function)
        {
            var parameters = function.Method.GetParameters();
            return parameters.Length == 1 ? parameters[0].ParameterType : null;
        }

        object BuildInternal(Type signature)
        {
            object app;
            if (!_properties.TryGetValue("builder.DefaultApp", out app))
            {
                app = NotFound;
            }

            foreach (var middleware in _middleware.Reverse())
            {
                var neededSignature = middleware.Item1;
                var middlewareDelegate = middleware.Item2;
                var middlewareArgs = middleware.Item3;

                app = Convert(neededSignature, app);
                var invokeParameters = new[] { app }.Concat(middlewareArgs).ToArray();
                app = middlewareDelegate.DynamicInvoke(invokeParameters);
                app = Convert(neededSignature, app);
            }

            return Convert(signature, app);
        }

        readonly Func<
            IDictionary<string, object>,
            IDictionary<string, string[]>,
            Stream,
            Task<ResultTuple>> NotFound = (_, __, ___) =>
                TaskHelpers.FromResult(new ResultTuple(
                    new Dictionary<string, object>(),
                    404,
                    new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                    output => TaskHelpers.Completed()));

        object Convert(Type signature, object app)
        {
            if (app == null)
            {
                return null;
            }
            if (signature.IsInstanceOfType(app))
            {
                return app;
            }
            if (typeof(Delegate).IsAssignableFrom(signature))
            {
                var memberDelegate = ToMemberDelegate(signature, app);
                if (memberDelegate != null)
                {
                    return memberDelegate;
                }
            }
            foreach (var conversion in _conversions)
            {
                var returnType = conversion.Key.Item1;
                var parameterType = conversion.Key.Item2;
                if (parameterType.IsInstanceOfType(app) &&
                    signature.IsAssignableFrom(returnType))
                {
                    return conversion.Value.DynamicInvoke(app);
                }
            }
            var owinConversion = Conversions.EmitConversion(app.GetType(), signature);
            if (owinConversion != null)
            {
                var key = Tuple.Create(signature, app.GetType());
                _conversions[key] = owinConversion;
                return owinConversion(app);
            }
            throw new ApplicationException("No conversion available");
        }

        Delegate ToMemberDelegate(Type signature, object app)
        {
            var signatureMethod = signature.GetMethod("Invoke");
            var signatureParameters = signatureMethod.GetParameters();

            var methods = app.GetType().GetMethods();
            foreach (var method in methods)
            {
                if (method.Name != "Invoke" && method.Name != "App")
                {
                    continue;
                }
                var methodParameters = method.GetParameters();
                if (methodParameters.Length != signatureParameters.Length)
                {
                    continue;
                }
                if (methodParameters
                    .Zip(signatureParameters, (methodParameter, signatureParameter) => methodParameter.ParameterType.IsAssignableFrom(signatureParameter.ParameterType))
                    .Any(compatible => compatible == false))
                {
                    continue;
                }
                if (!signatureMethod.ReturnType.IsAssignableFrom(method.ReturnType))
                {
                    continue;
                }
                return Delegate.CreateDelegate(signature, app, method);
            }
            return null;
        }


        Tuple<Type, Delegate, object[]> ToMiddlewareFactory(object middlewareObject, object[] args)
        {
            var middlewareDelegate = middlewareObject as Delegate;
            if (middlewareDelegate == null)
            {
                var methods = middlewareObject.GetType().GetMethods();
                foreach (var method in methods)
                {
                    if (method.Name != "Invoke" && method.Name != "Middleware")
                    {
                        continue;
                    }
                    var parameters = method.GetParameters();
                    var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

                    if (parameterTypes.Length != args.Length + 1)
                    {
                        continue;
                    }
                    if (!parameterTypes
                        .Skip(1)
                        .Zip(args, TestArgForParameter)
                        .All(x => x))
                    {
                        continue;
                    }
                    var genericFuncTypes = parameterTypes.Concat(new[] { method.ReturnType });
                    var funcType = Expression.GetFuncType(genericFuncTypes.ToArray());
                    middlewareDelegate = Delegate.CreateDelegate(funcType, middlewareObject, method);
                    return Tuple.Create(parameters[0].ParameterType, middlewareDelegate, args);
                }
            }
            if (middlewareDelegate == null && middlewareObject is Type)
            {
                var middlewareType = middlewareObject as Type;
                var constructors = middlewareType.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
                    if (parameterTypes.Length != args.Length + 1)
                    {
                        continue;
                    }
                    if (!parameterTypes
                        .Skip(1)
                        .Zip(args, TestArgForParameter)
                        .All(x => x))
                    {
                        continue;
                    }

                    var parameterExpressions = parameters.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
                    var callConstructor = Expression.New(constructor, parameterExpressions);
                    middlewareDelegate = Expression.Lambda(callConstructor, parameterExpressions).Compile();
                    return Tuple.Create(parameters[0].ParameterType, middlewareDelegate, args);
                }
            }
            //if (middlewareDelegate == null)
            //{
            //    return new Func<object, object>(_ => middlewareObject);
            //}

            return Tuple.Create(GetParameterType(middlewareDelegate), middlewareDelegate, args);
        }

        static bool TestArgForParameter(Type parameterType, object arg)
        {
            return (arg == null && !parameterType.IsValueType) ||
                parameterType.IsInstanceOfType(arg);
        }
    }
}
