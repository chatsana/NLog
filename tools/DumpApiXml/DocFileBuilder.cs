﻿namespace DumpApiXml
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml;

    public class DocFileBuilder
    {
        private static Dictionary<string, string> simpleTypeNames = new Dictionary<string, string>()
        {
            { typeof(string).FullName, "String" },
            { typeof(int).FullName, "Integer" },
            { typeof(long).FullName, "Long" },
            { typeof(bool).FullName, "Boolean" },
            { typeof(char).FullName, "Char" },
            { typeof(byte).FullName, "Byte" },
            { typeof(CultureInfo).FullName, "Culture" },
            { typeof(Encoding).FullName, "Encoding" },
            { "NLog.Layouts.Layout", "Layout" },
            { "NLog.Targets.Target", "Target" },
            { "NLog.Conditions.ConditionExpression", "Condition" },
            { "NLog.Filters.FilterResult", "FilterResult" },
            { "NLog.Layout", "Layout" },
            { "NLog.Target", "Target" },
            { "NLog.ConditionExpression", "Condition" },
            { "NLog.FilterResult", "FilterResult" },
            { "NLog.TargetCollection", "Target" },
            { "NLog.Targets.NLogViewerParameterInfoCollection", "NLog.Targets.NLogViewerParameterInfo" },
            { "NLog.Win32.Targets.ConsoleRowHighlightingRuleCollection", "NLog.Targets.ConsoleRowHighlightingRule" },
            { "NLog.Win32.Targets.ConsoleWordHighlightingRuleCollection", "NLog.Targets.ConsoleWordHighlightingRule" },
            { "NLog.Layouts.CsvLayout+ColumnDelimiterMode", "NLog.Layouts.ColumnDelimiterMode" },
            { "NLog.RichTextBoxRowColoringRuleCollection", "NLog.Targets.RichTextBoxRowColoringRuleCollection" },
            { "NLog.Targets.Wrappers.FilteringRuleCollection", "NLog.Targets.Wrappers.FilteringRule" },
            { "NLog.Targets.RichTextBoxWordColoringRuleCollection", "NLog.Targets.RichTextBoxWordColoringRule" },
            { "NLog.Targets.MethodCallParameterCollection", "NLog.Targets.MethodCallParameter" },
            { "NLog.Targets.RichTextBoxRowColoringRuleCollection", "NLog.Targets.RichTextBoxRowColoringRule" },
            { "NLog.Targets.WebServiceTarget+WebServiceProtocol", "NLog.Targets.WebServiceProtocol" },
            { "NLog.Targets.DatabaseParameterInfoCollection", "NLog.Targets.DatabaseParameterInfo" },
        };

        private List<Assembly> assemblies = new List<Assembly>();
        private List<XmlDocument> comments = new List<XmlDocument>();
        private List<string> referenceAssemblyDirectories = new List<string>();

        public void AddReferenceDirectory(string dir)
        {
            this.referenceAssemblyDirectories.Add(dir);
        }

        public void Build(string outputFile)
        {
            using (XmlWriter writer = XmlWriter.Create(outputFile, new XmlWriterSettings
            {
                Indent = true
            }))
            {
                Build(writer);
            }
        }

        public void Build(XmlWriter writer)
        {
            writer.WriteProcessingInstruction("xml-stylesheet", "type='text/xsl' href='style.xsl'");
            writer.WriteStartElement("types");
            this.DumpApiDocs(writer, "target", "NLog.Targets.TargetAttribute", "", " target");
            this.DumpApiDocs(writer, "layout", "NLog.Layouts.LayoutAttribute", "", "");
            this.DumpApiDocs(writer, "layout-renderer", "NLog.LayoutRenderers.LayoutRendererAttribute", "${", "}");
            this.DumpApiDocs(writer, "filter", "NLog.Filters.FilterAttribute", "", " filter");

            this.DumpApiDocs(writer, "target", "NLog.TargetAttribute", "", " target");
            this.DumpApiDocs(writer, "layout", "NLog.LayoutAttribute", "", "");
            this.DumpApiDocs(writer, "layout-renderer", "NLog.LayoutRendererAttribute", "${", "}");
            this.DumpApiDocs(writer, "filter", "NLog.FilterAttribute", "", " filter");
            writer.WriteEndElement();
        }

        public void DisableComments()
        {
            this.comments.Clear();
        }

        public void LoadAssembly(string fileName)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(this.CurrentDomain_ReflectionOnlyAssemblyResolve);
            try
            {
                this.assemblies.Add(Assembly.ReflectionOnlyLoadFrom(fileName));
            }
            finally
            {
                //AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);
            }
        }

        public void LoadComments(string commentsFile)
        {
            var commentsDoc = new XmlDocument();
            commentsDoc.Load(commentsFile);
            FixWhitespace(commentsDoc.DocumentElement);
            this.comments.Add(commentsDoc);
        }

        private void FixWhitespace(XmlElement xmlElement)
        {
            foreach (var node in xmlElement.ChildNodes)
            {
                XmlElement el = node as XmlElement;
                if (el != null)
                {
                    FixWhitespace(el);
                    continue;
                }

                XmlText txt = node as XmlText;
                if (txt != null)
                {
                    txt.Value = FixWhitespace(txt.Value);
                }
            }
        }

        private string FixWhitespace(string p)
        {
            p = p.Replace("\n", " ");
            p = p.Replace("\r", " ");
            string oldP = "";

            while (oldP != p)
            {
                oldP = p;
                p = p.Replace("  ", " ");
            }

            return p;
        }

        private static string GetTypeName(Type type)
        {
            string simpleName;

            if (simpleTypeNames.TryGetValue(type.FullName, out simpleName))
            {
                return simpleName;
            }

            return type.FullName;
        }

        private static bool IsCollection(PropertyInfo prop, out Type elementType, out string elementName)
        {
            object v1, v2;

            if (TryGetFirstTwoArgumentForAttribute(prop, "NLog.Config.ArrayParameterAttribute", out v1, out v2))
            {
                elementType = (Type)v1;
                elementName = (string)v2;
                return true;
            }
            else
            {
                elementName = null;
                elementType = null;
                return false;
            }
        }

        private static bool TryGetFirstArgumentForAttribute(Type type, string attributeTypeName, out object value)
        {
            foreach (CustomAttributeData cad in CustomAttributeData.GetCustomAttributes(type))
            {
                if (cad.Constructor.DeclaringType.FullName == attributeTypeName)
                {
                    value = cad.ConstructorArguments[0].Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool TryGetFirstArgumentForAttribute(PropertyInfo propInfo, string attributeTypeName, out object value)
        {
            foreach (CustomAttributeData cad in CustomAttributeData.GetCustomAttributes(propInfo))
            {
                if (cad.Constructor.DeclaringType.FullName == attributeTypeName)
                {
                    value = cad.ConstructorArguments[0].Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool TryGetFirstTwoArgumentForAttribute(PropertyInfo propInfo, string attributeTypeName, out object value1, out object value2)
        {
            foreach (CustomAttributeData cad in CustomAttributeData.GetCustomAttributes(propInfo))
            {
                if (cad.Constructor.DeclaringType.FullName == attributeTypeName)
                {
                    value1 = cad.ConstructorArguments[0].Value;
                    value2 = cad.ConstructorArguments[1].Value;
                    return true;
                }
            }

            value1 = value2 = null;
            return false;
        }

        private static bool TryGetTypeNameFromNameAttribute(Type type, string attributeTypeName, out string name)
        {
            object val;
            if (TryGetFirstArgumentForAttribute(type, attributeTypeName, out val))
            {
                name = (string)val;
                return true;
            }

            name = null;
            return false;
        }

        private string Capitalize(string p)
        {
            return p.Substring(0, 1).ToUpper() + p.Substring(1);
        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            //Console.WriteLine("Resolving '{0}' {1}", args.Name, string.Join(";", this.assemblies.Select(c => c.ToString()).ToArray()));
            var asmName = new AssemblyName(args.Name);
            var assembly = this.assemblies.Where(c => c.GetName().Name == asmName.Name).FirstOrDefault();
            if (assembly != null)
            {
                Console.WriteLine("Resolved '{0}' from pre-loaded assembly '{1}'.", asmName, assembly.FullName);
                return assembly;
            }

            string assemblyFileName = asmName.Name + ".dll";
            foreach (string refDir in this.referenceAssemblyDirectories)
            {
                string fileName = Path.Combine(refDir, assemblyFileName);
                if (File.Exists(fileName))
                {
                    Console.WriteLine("Resolved '{0}' from file '{1}'.", asmName.Name, fileName);
                    return Assembly.ReflectionOnlyLoadFrom(fileName);
                }
            }

            Console.WriteLine("Could not resolve: {0}", assemblyFileName);
            throw new FileNotFoundException(assemblyFileName + " not found in reference assembly.");
        }

        private void DumpApiDocs(XmlWriter writer, string kind, string attributeTypeName, string titlePrefix, string titleSuffix)
        {
            foreach (Type type in this.GetTypesWithAttribute(attributeTypeName).OrderBy(t => t.Name))
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                writer.WriteStartElement("type");
                writer.WriteAttributeString("kind", kind);
                writer.WriteAttributeString("assembly", type.Assembly.GetName().Name);
                writer.WriteAttributeString("clrType", type.FullName);

                string name;
                if (TryGetTypeNameFromNameAttribute(type, attributeTypeName, out name))
                {
                    writer.WriteAttributeString("name", name);
                }
                writer.WriteAttributeString("slug", this.GetSlug(name, kind));
                writer.WriteAttributeString("title", titlePrefix + name + titleSuffix);
                if (HasAttribute(type, "NLog.Config.AdvancedAttribute"))
                {
                    writer.WriteAttributeString("advanced", "1");
                }

                if (InheritsFrom(type, "CompoundTargetBase"))
                {
                    writer.WriteAttributeString("iscompound", "1");
                }

                if (InheritsFrom(type, "WrapperTargetBase"))
                {
                    writer.WriteAttributeString("iswrapper", "1");
                }

                if (InheritsFrom(type, "WrapperLayoutRendererBase"))
                {
                    writer.WriteAttributeString("iswrapper", "1");
                }

                this.DumpTypeMembers(writer, type);

                writer.WriteEndElement();
            }
        }

        private bool InheritsFrom(Type type, string typeName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private void DumpTypeMembers(XmlWriter writer, Type type)
        {
            XmlElement memberDoc;
            var categories = new Dictionary<string, int>();

            categories["General Options"] = 10;
            categories["Layout Options"] = 20;

            if (this.TryGetMemberDoc("T:" + type.FullName, out memberDoc))
            {
                writer.WriteStartElement("doc");
                memberDoc.WriteContentTo(writer);
                writer.WriteEndElement();

                foreach (XmlElement element in memberDoc.SelectNodes("docgen/categories/category"))
                {
                    string categoryName = element.GetAttribute("name");
                    int order = Convert.ToInt32(element.GetAttribute("order"));

                    categories[categoryName] = order;
                }
            }

            var property2Category = new Dictionary<PropertyInfo, string>();
            var propertyOrderWithinCategory = new Dictionary<PropertyInfo, int>();
            var propertyDoc = new Dictionary<PropertyInfo, XmlElement>();

            foreach (PropertyInfo propInfo in
                this.GetProperties(type))
            {
                string category = null;
                int order = 100;

                if (this.TryGetMemberDoc(
                    "P:" + propInfo.DeclaringType.FullName + "." + propInfo.Name, out memberDoc))
                {
                    propertyDoc.Add(propInfo, memberDoc);

                    var docgen = (XmlElement)memberDoc.SelectSingleNode("docgen");
                    if (docgen != null)
                    {
                        category = docgen.GetAttribute("category");
                        order = Convert.ToInt32(docgen.GetAttribute("order"));
                    }
                }

                if (string.IsNullOrEmpty(category))
                {
                    Console.WriteLine("WARNING: Property {0}.{1} does not have <docgen /> element defined.",
                        propInfo.DeclaringType.Name, propInfo.Name);

                    category = "Other";
                }

                int categoryOrder;

                if (!categories.TryGetValue(category, out categoryOrder))
                {
                    categories.Add(category, 100 + categories.Count);
                }

                //Console.WriteLine("p: {0} cat: {1} order: {2}", propInfo.Name, category, order);
                property2Category[propInfo] = category;
                propertyOrderWithinCategory[propInfo] = order;
            }

            foreach (string category in categories.OrderBy(c => c.Value).Select(c => c.Key))
            {
                string categoryName = category;

                foreach (PropertyInfo propInfo in
                    this.GetProperties(type)
                        .Where(p => property2Category[p] == categoryName).OrderBy(
                            p => propertyOrderWithinCategory[p]).ThenBy(pi => pi.Name))
                {
                    if (propInfo.DeclaringType.FullName == "NLog.LayoutRenderer")
                    {
                        // skip properties from NLog.LayoutRenderer in NLog v1.0
                        // they have been moved to separate classes as wrappers
                        // continue;
                    }

                    string elementTag;
                    Type elementType;

                    writer.WriteStartElement("property");
                    writer.WriteAttributeString("name", propInfo.Name);
                    writer.WriteAttributeString("camelName", this.MakeCamelCase(propInfo.Name));
                    object val;

                    if (TryGetFirstArgumentForAttribute(
                        propInfo, "System.ComponentModel.DefaultValueAttribute", out val))
                    {
                        writer.WriteAttributeString(
                            "defaultValue", Convert.ToString(val, CultureInfo.InvariantCulture));
                    }

                    writer.WriteAttributeString("category", categoryName);

                    if (HasAttribute(propInfo, "NLog.Config.AdvancedAttribute"))
                    {
                        writer.WriteAttributeString("advanced", "1");
                    }

                    if (HasAttribute(propInfo, "NLog.Config.RequiredParameterAttribute"))
                    {
                        writer.WriteAttributeString("required", "1");
                    }
                    else
                    {
                        writer.WriteAttributeString("required", "0");
                    }

                    if (propInfo.Name == "Encoding")
                    {
                        writer.WriteAttributeString("type", "Encoding");
                    }
                    else if (HasAttribute(propInfo, "NLog.Config.AcceptsLayoutAttribute") ||
                             propInfo.Name == "Layout")
                    {
                        writer.WriteAttributeString("type", "Layout");
                    }
                    else if (HasAttribute(propInfo, "NLog.Config.AcceptsConditionAttribute") ||
                             propInfo.Name == "Condition")
                    {
                        writer.WriteAttributeString("type", "Condition");
                    }
                    else if (propInfo.PropertyType.IsEnum)
                    {
                        writer.WriteAttributeString("type", "Enum");
                        writer.WriteAttributeString("enumType", GetTypeName(propInfo.PropertyType));
                        foreach (FieldInfo fi in
                            propInfo.PropertyType.GetFields(BindingFlags.Static | BindingFlags.Public))
                        {
                            writer.WriteStartElement("enum");
                            writer.WriteAttributeString("name", fi.Name);
                            if (
                                this.TryGetMemberDoc(
                                    "F:" + propInfo.PropertyType.FullName.Replace("+", ".") + "." + fi.Name,
                                    out memberDoc))
                            {
                                writer.WriteStartElement("doc");
                                memberDoc.WriteContentTo(writer);
                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                        }
                    }
                    else if (IsCollection(propInfo, out elementType, out elementTag))
                    {
                        writer.WriteAttributeString("type", "Collection");
                        writer.WriteStartElement("elementType");
                        writer.WriteAttributeString("name", GetTypeName(elementType));
                        writer.WriteAttributeString("elementTag", elementTag);
                        this.DumpTypeMembers(writer, elementType);
                        writer.WriteEndElement();
                    }
                    else
                    {
                        writer.WriteAttributeString("type", GetTypeName(propInfo.PropertyType));
                    }

                    if (this.TryGetMemberDoc("P:" + propInfo.DeclaringType.FullName + "." + propInfo.Name, out memberDoc))
                    {
                        writer.WriteStartElement("doc");
                        memberDoc.WriteContentTo(writer);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }
            }
        }

        private void FixupElement(XmlElement element)
        {
            var summary = (XmlElement)element.SelectSingleNode("summary");
            if (summary != null)
            {
                summary.InnerXml = this.FixupSummaryXml(summary.InnerXml);
            }

            foreach (XmlElement code in element.SelectNodes("//code[@src]"))
            {
                code.SetAttribute("source", code.GetAttribute("src"));
                code.RemoveAttribute("src");
            }
        }

        private string FixupSummaryXml(string xml)
        {
            xml = xml.Trim();
            xml = this.ReplaceAndCapitalize(xml, "Gets or sets a value indicating ", "Indicates ");
            xml = this.ReplaceAndCapitalize(xml, "Gets or sets the ", "");
            xml = this.ReplaceAndCapitalize(xml, "Gets or sets a ", "");
            xml = this.ReplaceAndCapitalize(xml, "Gets or sets ", "");
            xml = this.ReplaceAndCapitalize(xml, "Gets the ", "The ");
            return xml;
        }

        private int GetCategoryOrder(PropertyInfo propInfo, Dictionary<string, int> orders)
        {
            XmlElement memberDoc;

            if (this.TryGetMemberDoc("P:" + propInfo.DeclaringType.FullName + "." + propInfo.Name, out memberDoc))
            {
                string category = null;
                var docgen = (XmlElement)memberDoc.SelectSingleNode("docgen");
                if (docgen != null)
                {
                    category = docgen.GetAttribute("category");
                }

                if (string.IsNullOrEmpty(category))
                {
                    category = "General";
                }

                int categoryOrder;
                if (!orders.TryGetValue(category, out categoryOrder))
                {
                    categoryOrder = 100 + orders.Count;
                    orders.Add(category, categoryOrder);
                }

                return categoryOrder;
            }

            return 50;
        }

        private int GetOrder(PropertyInfo propInfo)
        {
            XmlElement memberDoc;

            if (this.TryGetMemberDoc("P:" + propInfo.DeclaringType.FullName + "." + propInfo.Name, out memberDoc))
            {
                var docgen = (XmlElement)memberDoc.SelectSingleNode("docgen");
                if (docgen != null)
                {
                    string order = docgen.GetAttribute("order");
                    if (!string.IsNullOrEmpty(order))
                    {
                        return Convert.ToInt32(order);
                    }
                }
            }

            return 100;
        }

        private IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            return type.GetProperties()
                .Where(c => this.IncludeProperty(c));
        }

        private string GetSlug(string name, string kind)
        {
            switch (kind)
            {
                case "target":
                    return name + "_target";

                case "layout-renderer":
                    return name + "_layout_renderer";

                case "layout":
                    return name;

                case "filter":
                    return name + "_filter";
            }

            string slugBase;

            if (name == name.ToUpperInvariant())
            {
                slugBase = name.ToLowerInvariant();
            }
            else
            {
                name = name.Replace("NLog", "Nlog");
                name = name.Replace("Log4J", "Log4j");

                var sb = new StringBuilder();
                for (int i = 0; i < name.Length; ++i)
                {
                    if (Char.IsUpper(name[i]) && i > 0)
                    {
                        sb.Append("_");
                    }
                    sb.Append(name[i]);
                }

                slugBase = sb.ToString();
            }

            if (kind == "layout")
            {
                return slugBase;
            }
            else
            {
                return slugBase + "-" + kind;
            }
        }

        private IEnumerable<Type> GetTypesWithAttribute(string attributeTypeName)
        {
            foreach (Assembly assembly in this.assemblies)
            {
                foreach (Type t in assembly.GetTypes())
                {
                    if (HasAttribute(t, attributeTypeName))
                    {
                        yield return t;
                    }
                }
            }
        }

        private bool HasAttribute(Type type, string attributeTypeName)
        {
            foreach (CustomAttributeData cad in CustomAttributeData.GetCustomAttributes(type))
            {
                if (cad.Constructor.DeclaringType.FullName == attributeTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAttribute(PropertyInfo propertyInfo, string attributeTypeName)
        {
            foreach (CustomAttributeData cad in CustomAttributeData.GetCustomAttributes(propertyInfo))
            {
                if (cad.Constructor.DeclaringType.FullName == attributeTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IncludeProperty(PropertyInfo pi)
        {
            if (pi.CanRead && pi.CanWrite && pi.GetSetMethod() != null && pi.GetSetMethod().IsPublic)
            {
                if (pi.Name == "CultureInfo")
                {
                    return false;
                }

                if (pi.Name == "WrappedTarget")
                {
                    return false;
                }

                if ((pi.PropertyType.FullName.StartsWith("NLog.ILayout") || pi.PropertyType.FullName.StartsWith("NLog.Layout")) && pi.Name != "Layout" && pi.Name.EndsWith("Layout"))
                {
                    return false;
                }

                if (pi.PropertyType.FullName.StartsWith("NLog.Conditions.ConditionExpression") && pi.Name != "Condition" && pi.Name.EndsWith("Condition"))
                {
                    return false;
                }

                if (pi.Name.StartsWith("Compiled"))
                {
                    return false;
                }

                return true;
            }

            if (HasAttribute(pi, "NLog.Config.ArrayParameterAttribute"))
            {
                return true;
            }

            return false;
        }

        private string MakeCamelCase(string s)
        {
            if (s.Length <= 2)
            {
                return s.ToLowerInvariant();
            }

            if (s.ToUpperInvariant() == s)
            {
                return s.ToLowerInvariant();
            }

            if (s.StartsWith("DB"))
            {
                return "db" + s.Substring(2);
            }

            return s.Substring(0, 1).ToLowerInvariant() + s.Substring(1);
        }

        private string ReplaceAndCapitalize(string xml, string pattern, string replacement)
        {
            if (xml.StartsWith(pattern))
            {
                return this.Capitalize(xml.Replace(pattern, replacement));
            }
            return xml;
        }

        private bool TryGetMemberDoc(string id, out XmlElement element)
        {
            foreach (XmlDocument doc in this.comments)
            {
                element = (XmlElement)doc.SelectSingleNode("/doc/members/member[@name='" + id + "']");
                if (element != null)
                {
                    this.FixupElement(element);
                    return true;
                }
            }

            element = null;
            return false;
        }
    }
}