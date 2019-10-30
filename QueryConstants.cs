using Giny.Core.IO;
using Giny.ORM.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Giny.ORM
{
    class QueryConstants
    {
        public const string CREATE_TABLE = "CREATE TABLE if not exists {0} ({1})";

        public const string DROP_TABLE = "DROP TABLE IF EXISTS {0}";

        public const string DELETE_TABLE = "DELETE FROM {0}";

        public const string SELECT = "SELECT * FROM `{0}`";

        public const string SELECT_WHERE = "SELECT * FROM `{0}` WHERE {1}";

        public const string COUNT_CONDITIONAL = "SELECT COUNT(*) FROM `{0}` WHERE {1}";

        public const string COUNT = "SELECT COUNT(*) FROM `{0}`";

        public const string INSERT = "INSERT INTO `{0}` VALUES {1}";

        public const string UPDATE = "UPDATE `{0}` SET {1} WHERE {2} = {3}";

        public const string REMOVE = "DELETE FROM `{0}` WHERE `{1}` = {2}";

        public static string ConvertType(PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<TypeOverrideAttribute>();
            if (attribute != null)
            {
                return attribute.NewType;
            }
            if (property.GetCustomAttribute<ProtoSerializeAttribute>() != null)
            {
                return "BLOB";
            }
            switch (property.PropertyType.Name)
            {
                case "String":
                    return "VARCHAR(255)";
                case "Int16":
                    return "SMALLINT";
                case "Int32":
                    return "INT";
                case "Int64":
                    return "BIGINT";
                case "Byte":
                    return "TINYINT";
            }

            return "mediumtext";
        }
    }
}
