﻿using System;
using System.Threading;
using Microsoft.Practices.ServiceLocation;
using NUnit.Framework;
using SolrNet.Utils;

namespace SolrNet.Tests {
    [TestFixture]
    public class CommonServiceLocatorTests {
        [Test]
        public void Transient() {
            var container = new Container();
            container.Register<IService>(c => new ServiceImpl());
            var inst1 = container.GetInstance<IService>();
            var inst2 = container.GetInstance<IService>();
            Assert.AreNotSame(inst1, inst2);
        }

        [Test]
        public void Singleton() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>(c => inst);
            var inst1 = container.GetInstance<IService>();
            var inst2 = container.GetInstance<IService>();
            Assert.AreSame(inst, inst1);
            Assert.AreSame(inst, inst2);
        }

        [Test]
        public void PerThread() {
            var container = new Container();
            container.Register(c => servicePerThread);
            var id = ((ServiceImpl) container.GetInstance<IService>()).Id;
            var t = new Thread(() => {
                try {
                    var id2 = ((ServiceImpl) container.GetInstance<IService>()).Id;
                    Assert.AreNotEqual(id, id2);
                } catch (Exception ex) {
                    Assert.Fail(ex.ToString());
                }
            });
            t.Start();
            t.Join();
        }

        [Test]
        public void PerThread_with_helper() {
            var container = new Container();
            container.Register(c => ThreadLocal<IService>.Set(() => new ServiceImpl()));
            var id = ((ServiceImpl) container.GetInstance<IService>()).Id;
            var id3 = ((ServiceImpl) container.GetInstance<IService>()).Id;
            Assert.AreEqual(id, id3);
            var t = new Thread(() => {
                try {
                    var id2 = ((ServiceImpl) container.GetInstance<IService>()).Id;
                    Assert.AreNotEqual(id, id2);
                } catch (Exception ex) {
                    Assert.Fail(ex.ToString());
                }
            });
            t.Start();
            t.Join();
        }

        public class ThreadLocal<T> where T : class {
            [ThreadStatic]
            private static T instance;

            public delegate S FactoryDelegate<S>();

            private static FactoryDelegate<T> factory;

            public static T Set(FactoryDelegate<T> factoryMethod) {
                factory = factoryMethod;
                return Instance;
            }

            public static T Instance {
                get {
                    if (instance == null)
                        instance = factory();
                    return instance;
                }
            }
        }

        [ThreadStatic]
        public static IService service;

        public static IService servicePerThread {
            get {
                if (service == null)
                    service = new ServiceImpl();
                return service;
            }
        }

        [Test]
        public void NoInterface() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register(c => inst);
            var inst1 = container.GetInstance<ServiceImpl>();
            Assert.AreSame(inst, inst1);
        }

        [Test]
        [ExpectedException(typeof (ActivationException))]
        public void NoInterface_ask_for_interface_throws() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register(c => inst);
            container.GetInstance<IService>();
        }

        [Test]
        public void Injection() {
            var container = new Container();
            container.Register(c => new AnotherService(c.GetInstance<IService>()));
            var inst = new ServiceImpl();
            container.Register<IService>(c => inst);
            var svc = container.GetInstance<AnotherService>();
            Assert.AreSame(inst, svc.Svc);
        }

        [Test]
        [ExpectedException(typeof (ActivationException))]
        public void InjectionWithoutDependency_throws() {
            var container = new Container();
            container.Register(c => new AnotherService(c.GetInstance<IService>()));
            var svc = container.GetInstance<AnotherService>();
        }

        [Test]
        [ExpectedException(typeof (ApplicationException))]
        public void MultipleInstancesOfSameService_without_key_throws() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>(c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
        }

        [Test]
        public void MultipleInstancesOfSameService_with_key_resolves_first_by_type() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            var svc = container.GetInstance<IService>();
            Assert.AreSame(inst, svc);
        }

        [Test]
        public void CopyContainer() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            var newContainer = new Container(container);
            Assert.AreSame(container.GetInstance<IService>("inst1"), newContainer.GetInstance<IService>("inst1"));
            Assert.AreSame(container.GetInstance<IService>(), newContainer.GetInstance<IService>());
        }

        [Test]
        public void CopyContainer_doesnt_alter_original_container() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            var newContainer = new Container(container);
            newContainer.Register<IService>("inst2", c => inst);
            try {
                container.GetInstance<IService>("inst2");
                Assert.Fail("The original container has been modified!");
            } catch (ActivationException) {}
            newContainer.GetInstance<IService>("inst2");
        }

        [Test]
        [ExpectedException(typeof (ActivationException))]
        public void RemoveAllByType() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            container.RemoveAll<IService>();
            container.GetInstance<IService>();
        }

        [Test]
        public void RemoveByType() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            container.Remove<IService>();
            Assert.AreSame(inst, container.GetInstance<IService>());
        }

        [Test]
        public void RemoveByTypeAndKey() {
            var container = new Container();
            var inst = new ServiceImpl();
            container.Register<IService>("inst1", c => inst);
            var inst2 = new ServiceImpl();
            container.Register<IService>(c => inst2);
            container.Remove<IService>("inst1");
            Assert.AreSame(inst2, container.GetInstance<IService>());
        }

        public interface IService {}

        public class ServiceImpl : IService {
            private static readonly Random rnd = new Random();
            private readonly int id = rnd.Next();

            public int Id {
                get { return id; }
            }
        }

        public class AnotherService {
            private readonly IService svc;

            public IService Svc {
                get { return svc; }
            }

            public AnotherService(IService svc) {
                this.svc = svc;
            }
        }
    }
}