using MySql.Data.MySqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using Giny;
using Giny.ORM.Interfaces;
using Giny.ORM.Attributes;
using Giny.Core;
using System.Globalization;
using Giny.Core.IO;
using System.Text;
using System.Diagnostics;
using Giny.ORM.Expressions;

namespace Giny.ORM.IO
{
    public class DatabaseWriter
    {
        private string TableName
        {
            get;
            set;
        }
        private PropertyInfo[] AddProperties
        {
            get;
            set;
        }
        private PropertyInfo[] UpdateProperties
        {
            get;
            set;
        }
        private PropertyInfo PrimaryProperty
        {
            get;
            set;
        }
        private Dictionary<Type, MethodInfo> CustomSerializationMethods
        {
            get;
            set;
        }
        private Type Type
        {
            get;
            set;
        }
        public bool HasNoUpdateProperties
        {
            get;
            set;
        }
        public DatabaseWriter(Type type)
        {
            this.Type = type;
            var definition = TableManager.Instance.GetDefinition(type);
            this.AddProperties = GetAddProperties(type);
            this.UpdateProperties = GetUpdateProperties(type);
            this.TableName = definition.TableAttribute.TableName;
            this.PrimaryProperty = definition.PrimaryProperty;
            this.HasNoUpdateProperties = UpdateProperties.Length == 0;
            this.CustomSerializationMethods = definition.CustomSerializationMethods;
        }
        public void Use(ITable[] elements, DatabaseAction action)
        {
            lock (DatabaseManager.SyncRoot)
            {
                switch (action)
                {
                    case DatabaseAction.Add:
                        this.AddElements(elements);
                        return;

                    case DatabaseAction.Update:
                        this.UpdateElements(elements);
                        return;
                    case DatabaseAction.Remove:
                        this.DeleteElements(elements);
                        return;

                }
            }
        }

        private void AddElements(ITable[] elements)
        {
            var command = new MySqlCommand(string.Empty, DatabaseManager.Instance.UseProvider());

            List<string> final = new List<string>();

            for (int i = 0; i < elements.Length; i++)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("(");

                foreach (var property in AddProperties)
                {
                    sb.Append(string.Format("?{0}{1},", property.Name, i));
                    MySqlParameter mySQLParam = new MySqlParameter("?" + property.Name + i, ConvertObject(property, elements[i]));
                    command.Parameters.Add(mySQLParam);
                }

                sb = sb.Remove(sb.Length - 1, 1);
                sb.Append(")");

                final.Add(sb.ToString());
            }

            command.CommandText = string.Format(QueryConstants.INSERT, TableName, string.Format("{0}", string.Join(",", final)));

            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to add element to database (" + TableName + ") " +
                     ex.Message);
            }
        }

        private void UpdateElements(ITable[] elements)
        {
            if (HasNoUpdateProperties)
            {
                throw new Exception("Unable to update elements. " + TableName + " has no update property.");
            }


            var command = new MySqlCommand(string.Empty, DatabaseManager.Instance.UseProvider());

            List<string> final = new List<string>();

            for (int i = 0; i < elements.Length; i++)
            {
                StringBuilder sb = new StringBuilder();

                foreach (var property in UpdateProperties)
                {
                    sb.Append(string.Format("{2} = ?{0}{1},", property.Name, i, property.Name));
                    MySqlParameter mySQLParam = new MySqlParameter("?" + property.Name + i, ConvertObject(property, elements[i]));
                    command.Parameters.Add(mySQLParam);
                }

                sb = sb.Remove(sb.Length - 1, 1);

                final.Add(sb.ToString());

                var finalText = string.Format("{0}", string.Join(",", final));

                string arg1 = TableName;
                string arg2 = finalText;
                command.CommandText = string.Format(QueryConstants.UPDATE, arg1, arg2, PrimaryProperty.Name, elements[i].Id.ToString());
               
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable update database element (" + TableName + ") " +
                         ex.Message);
                }
            }



        }
        private void DeleteElements(IEnumerable<ITable> elements)
        {
            foreach (var element in elements)
            {
                var commandString = string.Format(QueryConstants.REMOVE, TableName, PrimaryProperty.Name, PrimaryProperty.GetValue(element));

                using (var command = new MySqlCommand(commandString, DatabaseManager.Instance.UseProvider()))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private object ConvertObject(PropertyInfo property, ITable element)
        {
            var value = property.GetValue(element);

            if (value == null)
            {
                return null;
            }
            if (property.PropertyType == typeof(DateTime))
            {
                value = ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (property.PropertyType == typeof(Boolean))
            {
                value = Convert.ToByte(value);
            }
            else if (property.CustomAttributes.Count() > 0 && property.GetCustomAttribute<ProtoSerializeAttribute>() != null)
            {
                byte[] content = Protobuf.Serialize(value);
                return content;
            }
            else if (CustomSerializationMethods.ContainsKey(property.PropertyType))
            {
                value = CustomSerializationMethods[property.PropertyType].Invoke(null, new object[] { value });
            }
            else if (value is Enum)
            {
                value = value.ToString();
            }
            else if (property.PropertyType.IsGenericType)
            {
                List<object> results = new List<object>();

                Type genericType = property.PropertyType.GetGenericTypeDefinition();

                if (genericType == typeof(List<>))
                {
                    var values = (IList)value;

                    foreach (var v in values)
                    {
                        results.Add(v);
                    }
                    return string.Join(",", results);
                }
                else
                {
                    throw new Exception("Unhandled generic type " + property.PropertyType.Name);
                }
            }

            return value;
        }

        public static PropertyInfo[] GetUpdateProperties(Type type)
        {
            return type.GetProperties().Where(property => property.GetCustomAttribute(typeof(IgnoreAttribute)) == null
            && property.GetCustomAttribute(typeof(UpdateAttribute), false) != null).OrderBy(x => x.MetadataToken).ToArray();
        }
        public static PropertyInfo[] GetAddProperties(Type type)
        {
            return type.GetProperties().Where(property => property.GetCustomAttribute(typeof(IgnoreAttribute)) == null).OrderBy(x => x.MetadataToken).ToArray();
        }

        public static void Update<T>(T item) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(new ITable[] { item }, DatabaseAction.Update);
        }

        public static void Update<T>(IEnumerable<T> items) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(items.Cast<ITable>().ToArray(), DatabaseAction.Update);
        }

        public static void Insert<T>(T item) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(new ITable[] { item }, DatabaseAction.Add);
        }
        public static void Insert<T>(IEnumerable<T> items) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(items.Cast<ITable>().ToArray(), DatabaseAction.Add);
        }

        public static void Remove<T>(T item) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(new ITable[] { item }, DatabaseAction.Remove);
        }
        public static void Remove<T>(IEnumerable<T> items) where T : ITable
        {
            TableManager.Instance.GetWriter(typeof(T)).Use(items.Cast<ITable>().ToArray(), DatabaseAction.Remove);

        }
        public static void CreateTable(Type type)
        {
            DatabaseManager.Instance.CreateTableIfNotExists(type);
        }

    }

    public enum DatabaseAction
    {
        Add,
        Update,
        Remove
    }
}
