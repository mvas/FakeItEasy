namespace FakeItEasy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq.Expressions;
    using FakeItEasy.Configuration;
    using FakeItEasy.Core;
    using FakeItEasy.Creation;
    using FakeItEasy.Creation.CastleDynamicProxy;
    using FakeItEasy.Creation.DelegateProxies;
    using FakeItEasy.Expressions;
    using FakeItEasy.IoC;
    using FakeItEasy.SelfInitializedFakes;

    /// <summary>
    /// Handles the registration of root dependencies in an IoC-container.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Container configuration.")]
    internal class RootModule
        : Module
    {
        /// <summary>
        /// Registers the dependencies.
        /// </summary>
        /// <param name="container">The container to register the dependencies in.</param>
        [SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode", Justification = "Container configuration.")]
        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Container configuration.")]
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Container configuration.")]
        public override void RegisterDependencies(DictionaryContainer container)
        {
            container.RegisterSingleton(c =>
                new DynamicOptionsBuilder(
                    c.Resolve<IEnumerable<IFakeOptionsBuilder>>()));

            container.RegisterSingleton(c =>
                new DynamicDummyFactory(
                    c.Resolve<IEnumerable<IDummyFactory>>()));

            container.RegisterSingleton<IExpressionCallMatcherFactory>(c =>
                new ExpressionCallMatcherFactory
                    {
                        Container = c
                    });

            container.RegisterSingleton(c =>
                new ExpressionArgumentConstraintFactory(c.Resolve<IArgumentConstraintTrapper>()));

            container.RegisterSingleton<ExpressionCallRule.Factory>(c =>
                callSpecification => new ExpressionCallRule(new ExpressionCallMatcher(callSpecification, c.Resolve<ExpressionArgumentConstraintFactory>(), c.Resolve<MethodInfoManager>(), c.Resolve<ICallExpressionParser>())));

            container.RegisterSingleton(c =>
                new MethodInfoManager());

            container.Register<FakeAsserter.Factory>(c => calls => new FakeAsserter(calls, c.Resolve<CallWriter>()));

            container.RegisterSingleton<FakeManager.Factory>(c =>
                (fakeObjectType, proxy) => new FakeManager(fakeObjectType, proxy));

            container.RegisterSingleton<FakeCallProcessorProvider.Factory>(c =>
                (typeOfFake, proxyOptions) =>
                    new FakeManagerProvider(c.Resolve<FakeManager.Factory>(), c.Resolve<IFakeManagerAccessor>(), typeOfFake, proxyOptions));

            container.RegisterSingleton<IFakeObjectCallFormatter>(c =>
                new DefaultFakeObjectCallFormatter(c.Resolve<ArgumentValueFormatter>(), c.Resolve<IFakeManagerAccessor>()));

            container.RegisterSingleton(c =>
                new ArgumentValueFormatter(c.Resolve<IEnumerable<IArgumentValueFormatter>>()));

            container.RegisterSingleton(c =>
                new CallWriter(c.Resolve<IFakeObjectCallFormatter>(), c.Resolve<IEqualityComparer<IFakeObjectCall>>()));

            container.RegisterSingleton<RecordingManager.Factory>(c =>
                x => new RecordingManager(x));

            container.RegisterSingleton<IFileSystem>(c =>
                new FileSystem());

            container.RegisterSingleton<FileStorage.Factory>(c =>
                x => new FileStorage(x, c.Resolve<IFileSystem>()));

            container.RegisterSingleton<ICallExpressionParser>(c =>
                new CallExpressionParser());

            container.RegisterSingleton<IExpressionParser>(c =>
                new ExpressionParser(c.Resolve<ICallExpressionParser>()));

            container.Register<IFakeCreatorFacade>(c =>
                new DefaultFakeCreatorFacade(c.Resolve<IFakeAndDummyManager>()));

            container.Register<IFakeAndDummyManager>(c =>
                                                         {
                                                             var fakeCreator = new FakeObjectCreator(c.Resolve<IProxyGenerator>(), c.Resolve<IExceptionThrower>(), c.Resolve<FakeCallProcessorProvider.Factory>());
                                                             var session = new DummyValueCreationSession(c.Resolve<DynamicDummyFactory>(), new SessionFakeObjectCreator { Creator = fakeCreator });
                                                             var fakeConfigurator = c.Resolve<DynamicOptionsBuilder>();

                                                             return new DefaultFakeAndDummyManager(session, fakeCreator, fakeConfigurator);
                                                         });

            container.RegisterSingleton(c => new CastleDynamicProxyGenerator(c.Resolve<CastleDynamicProxyInterceptionValidator>()));

            container.RegisterSingleton(c => new DelegateProxyGenerator());

            container.RegisterSingleton<IProxyGenerator>(c => new ProxyGeneratorSelector(c.Resolve<DelegateProxyGenerator>(), c.Resolve<CastleDynamicProxyGenerator>()));

            container.RegisterSingleton(
                c => new CastleDynamicProxyInterceptionValidator(c.Resolve<MethodInfoManager>()));

            container.RegisterSingleton<IExceptionThrower>(c => new DefaultExceptionThrower());

            container.RegisterSingleton<IFakeManagerAccessor>(c => new DefaultFakeManagerAccessor());

            container.Register(c =>
                new FakeFacade(c.Resolve<IFakeManagerAccessor>(), c.Resolve<IFixtureInitializer>()));

            container.Register<IFixtureInitializer>(c => new DefaultFixtureInitializer(c.Resolve<IFakeAndDummyManager>(), c.Resolve<ISutInitializer>()));

            container.RegisterSingleton<IEqualityComparer<IFakeObjectCall>>(c => new FakeCallEqualityComparer());

            container.Register<IInterceptionAsserter>(c => new DefaultInterceptionAsserter(c.Resolve<IProxyGenerator>()));

            container.Register<IArgumentConstraintTrapper>(c => new ArgumentConstraintTrap());

            container.Register<IArgumentConstraintManagerFactory>(c => new ArgumentConstraintManagerFactory());

            container.RegisterSingleton<IOutputWriter>(c => new DefaultOutputWriter(Console.Write));

            container.Register<ISutInitializer>(c => new DefaultSutInitializer(c.Resolve<IFakeAndDummyManager>()));

            container.RegisterSingleton(c => new EventHandlerArgumentProviderMap());

            container.Register(c => new SequentialCallContext(c.Resolve<CallWriter>()));
        }

        private class ExpressionCallMatcherFactory
            : IExpressionCallMatcherFactory
        {
            public DictionaryContainer Container { private get; set; }

            public ICallMatcher CreateCallMathcer(LambdaExpression callSpecification)
            {
                return new ExpressionCallMatcher(
                    callSpecification,
                    this.Container.Resolve<ExpressionArgumentConstraintFactory>(),
                    this.Container.Resolve<MethodInfoManager>(),
                    this.Container.Resolve<ICallExpressionParser>());
            }
        }

        private class FileSystem : IFileSystem
        {
            public Stream Open(string fileName, FileMode mode)
            {
                return File.Open(fileName, mode);
            }

            public bool FileExists(string fileName)
            {
                return File.Exists(fileName);
            }

            public void Create(string fileName)
            {
                File.Create(fileName).Dispose();
            }
        }

        private class SessionFakeObjectCreator
            : IFakeObjectCreator
        {
            public FakeObjectCreator Creator { private get; set; }

            public bool TryCreateFakeObject(Type typeOfFake, DummyValueCreationSession session, out object result)
            {
                result = this.Creator.CreateFake(typeOfFake, new ProxyOptions(), session, false);
                return result != null;
            }
        }

        private class ArgumentConstraintManagerFactory
            : IArgumentConstraintManagerFactory
        {
            public IArgumentConstraintManager<T> Create<T>()
            {
                return new DefaultArgumentConstraintManager<T>(ArgumentConstraintTrap.ReportTrappedConstraint);
            }
        }
    }
}
