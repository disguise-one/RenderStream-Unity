using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Disguise.RenderStream
{
    /// <summary>
    /// Creates a link.xml file to preserve type members required by <see cref="DisguiseRemoteParameters"/>, as it relies on reflection.
    /// This is to support managed code stripping, which is always enabled in IL2CPP: https://docs.unity3d.com/Manual/ManagedCodeStripping.html.
    /// </summary>
    class ReflectedMemberPreserver
    {
        const string k_XmlDirectory = "Library/Disguise";
        const string k_XmlName = "DisguisePreserve.xml";
        
        Dictionary<Assembly, Dictionary<Type, HashSet<MemberInfo>>> m_MembersToPreserve = new();

        public void Preserve(MemberInfo member)
        {
            var type = member.DeclaringType;
    
            if (type == null)
            {
                return;
            }
    
            var assembly = type.Assembly;
    
            if (!m_MembersToPreserve.TryGetValue(assembly, out var typeMembers))
            {
                typeMembers = new Dictionary<Type, HashSet<MemberInfo>>();
                m_MembersToPreserve.Add(assembly, typeMembers);
            }
            if (!typeMembers.TryGetValue(type, out var members))
            {
                members = new HashSet<MemberInfo>();
                typeMembers.Add(type, members);
            }
    
            members.Add(member);
        }

        public string GenerateAdditionalLinkXmlFile()
        {
            var projectDir = Path.GetDirectoryName(Application.dataPath);
            var xmlDir = $"{projectDir}/{k_XmlDirectory}";
            var xmlPath = $"{xmlDir}/{k_XmlName}";

            if (!Directory.Exists(xmlDir))
            {
                Directory.CreateDirectory(xmlDir);
            }
            
            CreateLinkXml(xmlPath);

            return xmlPath;
        }

        void CreateLinkXml(string filepath)
        {
            var xmlSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true
            };

            using var xmlWriter = XmlWriter.Create(filepath, xmlSettings);
            
            var xmlLinker = new XElement("linker");

            // Sort everything by name so the file output is deterministic. This ensures the build system
            // only detects a change when the preserved members are different.
            foreach (var assemblyMembers in m_MembersToPreserve.OrderBy(a => a.Key.GetName().Name))
            {
                var xmlAssembly = new XElement("assembly", new XAttribute("fullname", assemblyMembers.Key.GetName().Name));
                xmlLinker.Add(xmlAssembly);

                foreach (var typeMembers in assemblyMembers.Value.OrderBy(t => t.Key.FullName))
                {
                    var xmlType = new XElement("type", new XAttribute("fullname", ToCecilName(typeMembers.Key.FullName)));
                    xmlAssembly.Add(xmlType);

                    foreach (var member in typeMembers.Value.OrderBy(m => m.Name))
                    {
                        switch (member)
                        {
                            case FieldInfo field:
                            {
                                xmlType.Add(new XElement("field", new XAttribute("name", member.Name)));
                                break;
                            }
                            case PropertyInfo property:
                            {
                                xmlType.Add(new XElement("property", new XAttribute("name", member.Name)));
                                break;
                            }
                            case MethodInfo method:
                            {
                                xmlType.Add(new XElement("method", new XAttribute("name", member.Name)));
                                break;
                            }
                            default:
                            {
                                throw new NotImplementedException($"Unsupported {typeof(MemberInfo)} subtype");
                            }
                        }
                    }
                }
            }

            xmlLinker.Save(xmlWriter);
        }

        static string ToCecilName(string fullTypeName)
        {
            return fullTypeName.Replace('+', '/');
        }
    }
}
