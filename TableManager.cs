using Giny.Core.DesignPattern;
using Giny.ORM.Attributes;
using Giny.ORM.Expressions;
using Giny.ORM.Interfaces;
using Giny.ORM.IO;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Giny.ORM.Expressions.ExpressionsManager;

namespace Giny.ORM
{
    public class TableDefinitions
    {
        public FieldInfo Container;

        public IDictionary ContainerValue;

        public TableAttribute TableAttribute;

        public PropertyInfo[] Properties;

        public PropertyInfo PrimaryProperty;

        public Dictionary<Type, MethodInfo> CustomSerializationMethods;

        public Dictionary<Type, MethodInfo> CustomDeserializationMethods;

        public bool Load
        {
            get
            {
                return TableAttribute.Load;
            }
        }
        public TableDefinitions(Type type)
        {
            var attribute = type.GetCustomAttribute<TableAttribute>();

            if (attribute == null)
            {
                throw new Exception("Unable to find table attribute for table " + type.Name);
            }

            var field = type.GetFields(BindingFlags.NonPublic | BindingFlags.Static).FirstOrDefault(x => x.Name.ToLower() == attribute.TableName.ToLower());

            if ((field == null || !field.IsStatic || !field.FieldType.IsGenericType))
            {
                if (attribute.Load)
                    throw new Exception("Unable to find container for table : " + type.Name);
            }
            else
            {
                this.Container = field;
                this.ContainerValue = (IDictionary)Container.GetValue(null);
            }

            this.TableAttribute = attribute;
            this.Properties = type.GetProperties().Where(property =>
                property.GetCustomAttribute(typeof(IgnoreAttribute), false) == null).OrderBy(x => x.MetadataToken).ToArray();

            this.PrimaryProperty = GetPrimaryProperty();

            this.CustomSerializationMethods = GetCustomSerializationMethods(type);

            this.CustomDeserializationMethods = GetCustomDeserializationMethods(type);

        }
        private Dictionary<Type, MethodInfo> GetCustomSerializationMethods(Type tableType)
        {
            Dictionary<Type, MethodInfo> results = new Dictionary<Type, MethodInfo>();

            foreach (var method in tableType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<CustomSerializeAttribute>() != null)
                {
                    var type = method.GetParameters()[0].ParameterType;
                    results.Add(type, method);
                }
            }
            return results;
        }
        private Dictionary<Type, MethodInfo> GetCustomDeserializationMethods(Type tableType)
        {
            Dictionary<Type, MethodInfo> results = new Dictionary<Type, MethodInfo>();

            foreach (var method in tableType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<CustomDeserializeAttribute>() != null)
                {
                    var type = method.ReturnType;
                    results.Add(type, method);
                }
            }
            return results;
        }
        private PropertyInfo GetPrimaryProperty()
        {
            var properties = Properties.Where(property => property.GetCustomAttribute(typeof(PrimaryAttribute), false) != null);

            if (properties.Count() != 1)
            {
                if (properties.Count() == 0)
                    throw new Exception(string.Format("The Table '{0}' hasn't got a primary property", TableAttribute.TableName));

                if (properties.Count() > 1)
                    throw new Exception(string.Format("The Table '{0}' has too much primary properties", TableAttribute.TableName));
            }
            return properties.First();
        }

    }
    public class TableManager : Singleton<TableManager>
    {
        private Dictionary<Type, TableDefinitions> m_TableDefinitions = new Dictionary<Type, TableDefinitions>();

        private Dictionary<Type, DatabaseWriter> m_writers = new Dictionary<Type, DatabaseWriter>();

        public void Initialize(Type[] tableTypes)
        {
            foreach (var type in tableTypes)
            {
                m_TableDefinitions.Add(type, new TableDefinitions(type));
                m_writers.Add(type, new DatabaseWriter(type));
            }

        }
        public void RemoveFromContainer(ITable element)
        {
            var tableDefinition = m_TableDefinitions[element.GetType()];
            if (tableDefinition.Load)
                tableDefinition.ContainerValue.Remove(element.Id);
        }

        public void AddToContainer(ITable element)
        {
            var tableDefinition = m_TableDefinitions[element.GetType()];

            if (tableDefinition.Load)
                tableDefinition.ContainerValue.Add(element.Id, element);
        }
        public DatabaseWriter GetWriter(Type type)
        {
            return m_writers[type];
        }
        public TableDefinitions GetDefinition(Type type)
        {
            return m_TableDefinitions[type];
        }
    }
}
