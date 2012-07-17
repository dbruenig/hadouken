﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hadouken.Data;
using System.Linq.Expressions;
using NHibernate;
using NHibernate.Linq;
using FluentNHibernate.Cfg;
using Hadouken.Messaging;
using FluentNHibernate.Cfg.Db;
using System.Reflection;
using FluentNHibernate.Automapping;
using System.Configuration;
using FluentNHibernate.Conventions;
using System.IO;
using Hadouken.Configuration;
using NLog;

namespace Hadouken.Impl.Data
{
    internal class HdknAutomappingConfig : DefaultAutomappingConfiguration
    {
        public override bool ShouldMap(Type type)
        {
            return typeof(IModel).IsAssignableFrom(type);
        }
    }

    internal class EnumMappingConvention : IUserTypeConvention
    {
        public void Accept(FluentNHibernate.Conventions.AcceptanceCriteria.IAcceptanceCriteria<FluentNHibernate.Conventions.Inspections.IPropertyInspector> criteria)
        {
            criteria.Expect(x => x.Property.PropertyType.IsEnum);
        }

        public void Apply(FluentNHibernate.Conventions.Instances.IPropertyInstance instance)
        {
            instance.CustomType(instance.Property.PropertyType);
        }
    }

    public class FluentNhibernateDataRepository : IDataRepository
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private List<Assembly> _modelAssemblies = new List<Assembly>();

        private ISessionFactory _sessionFactory;
        private ISession _session;

        public FluentNhibernateDataRepository(IMessageBus mbus)
        {
            _modelAssemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(asm => asm.GetName().Name.StartsWith("Hadouken")));
            
            RebuildSessionFactory();
        }

        private void RebuildSessionFactory()
        {
            _logger.Info("Rebuilding session factory");

            string dataPath = HdknConfig.GetPath("Paths.Data");
            string connectionString = HdknConfig.ConnectionString.Replace("$Paths.Data$", dataPath);

            _sessionFactory = Fluently.Configure()
                .Database(SQLiteConfiguration.Standard.ConnectionString(connectionString))
                .Mappings(m =>
                {
                    m.AutoMappings.Add(AutoMap.Assemblies(new HdknAutomappingConfig(), _modelAssemblies).Conventions.Add(new EnumMappingConvention()));
                })
                .BuildSessionFactory();

            _session = _sessionFactory.OpenSession();
        }

        public void Save<TModel>(TModel instance) where TModel : IModel, new()
        {
            _session.Save(instance);
            _session.Flush();
        }

        public void Update<TModel>(TModel instance) where TModel : IModel, new()
        {
            _session.Update(instance);
            _session.Flush();
        }

        public void Delete<TModel>(TModel instance) where TModel : IModel, new()
        {
            _session.Delete(instance);
            _session.Flush();
        }

        public TModel Single<TModel>(int id) where TModel : IModel, new()
        {
            return _session.Load<TModel>(id);
        }

        public TModel Single<TModel>(Expression<Func<TModel, bool>> where) where TModel : IModel, new()
        {
            return _session.Query<TModel>().Where(where).SingleOrDefault();
        }

        public IList<TModel> List<TModel>() where TModel : IModel, new()
        {
            return _session.Query<TModel>().ToList();
        }

        public IList<TModel> List<TModel>(Expression<Func<TModel, bool>> where) where TModel : IModel, new()
        {
            return _session.Query<TModel>().Where(where).ToList();
        }
    }
}
