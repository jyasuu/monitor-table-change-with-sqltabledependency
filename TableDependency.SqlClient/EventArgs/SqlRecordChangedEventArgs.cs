﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TableDependency.Enums;
using TableDependency.EventArgs;
using TableDependency.Mappers;
using TableDependency.SqlClient.MessageTypes;

namespace TableDependency.SqlClient.EventArgs
{
    public sealed class SqlRecordChangedEventArgs<T> : RecordChangedEventArgs<T> where T : class
    {
        #region Member variables

        private const string Space = " ";
        private static readonly IEnumerable<PropertyInfo> _entiyProperiesInfo;

        private static readonly IList<Type> _types = new List<Type>()
        {
            typeof (string),
            typeof (short), typeof (short?),
            typeof (int), typeof (int?),
            typeof (long), typeof (long?),       
            typeof (decimal), typeof (decimal?),
            typeof (float), typeof (float?),
            typeof (DateTime), typeof (DateTime?),
            typeof (double), typeof (double?),
            typeof (bool), typeof (bool?)
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets modified record.
        /// </summary>
        /// <value>
        /// The changed entity.
        /// </value>
        public override T Entity { get; protected set; }

        /// <summary>
        /// Gets the change type (insert/delete/update).
        /// </summary>
        /// <value>
        /// The change action.
        /// </value>
        public override ChangeType ChangeType { get; protected set; }

        /// <summary>
        /// Gets SQL message type.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public override string MessageType { get; protected set; }

        #endregion

        #region Constructors

        static SqlRecordChangedEventArgs()
        {
            _entiyProperiesInfo = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField)
                .Where(propertyInfo => _types.Contains(propertyInfo.PropertyType) || (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                .ToArray();
        }

        internal SqlRecordChangedEventArgs(string messageType)
        {
            MessageType = messageType;
        }

        internal SqlRecordChangedEventArgs(string databaseObjectsNaming, string messageType, SqlXml message, ModelToTableMapper<T> mapper)
            : this(messageType)
        {
            Entity = MaterializeEntity(message, mapper);
            ChangeType = SetChangeType(databaseObjectsNaming, messageType);
        }

        #endregion

        #region Private methods

        private static ChangeType SetChangeType(string databaseObjectsNaming, string messageType)
        {
            if (messageType == string.Format(CustomMessageTypes.TemplateDeletedMessageType, databaseObjectsNaming))
                return ChangeType.Delete;
            if (messageType == string.Format(CustomMessageTypes.TemplateInsertedMessageType, databaseObjectsNaming))
                return ChangeType.Insert;
            if (messageType == string.Format(CustomMessageTypes.TemplateUpdatedMessageType, databaseObjectsNaming))
                return ChangeType.Update;

            return ChangeType.None;
        }

        private static T MaterializeEntity(SqlXml sqlXml, ModelToTableMapper<T> mapper)
        {
            var sqlXmlValue = sqlXml?.Value;
            if (string.IsNullOrWhiteSpace(sqlXmlValue)) return default(T);

            var stringXmlDocument = string.Concat("<Values xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">", sqlXmlValue, "</Values>");
            var xDocument = XDocument.Parse(stringXmlDocument);
            if (xDocument.Root == null) return default(T);

            var xElement = xDocument.Root.Elements().First();            
            var entity = (T)Activator.CreateInstance(typeof(T));

            foreach (var entityPropertyInfo in _entiyProperiesInfo)
            {
                var propertyMappedTo = mapper?.GetMapping(entityPropertyInfo);
                var propertyName = propertyMappedTo ?? entityPropertyInfo.Name;

                var xAttribute = xElement.Attributes().FirstOrDefault(a => string.Equals(NormalizeSpaceForColumnName(a.Name.ToString().ToLower()), propertyName.ToLower(), StringComparison.CurrentCultureIgnoreCase));
                if (xAttribute == default(XAttribute)) continue;

                var attributeValue = xAttribute.Value.Trim();
                var value = TypeDescriptor.GetConverter(entityPropertyInfo.PropertyType).ConvertFromString(attributeValue);
                entityPropertyInfo.SetValue(entity, value);
            }

            return entity;
        }

        private static string NormalizeSpaceForColumnName(string columnName)
        {
            return columnName.Replace("_x0020_", Space);
        }        

        #endregion
    }
}