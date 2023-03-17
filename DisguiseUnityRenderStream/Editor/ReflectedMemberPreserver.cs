using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
            var contents = CreateLinkXml();

            var projectDir = Application.dataPath.Replace("/Assets", string.Empty);
            var xmlDir = $"{projectDir}/{k_XmlDirectory}";
            var xmlPath = $"{xmlDir}/{k_XmlName}";

            if (!Directory.Exists(xmlDir))
            {
                Directory.CreateDirectory(xmlDir);
            }

            File.WriteAllText(xmlPath, contents);

            return xmlPath;
        }

        string CreateLinkXml()
        {
            var linkXml = new StringBuilder();
            
            linkXml.AppendLine("<linker>");
            
            // Sort everything by name so the file output is deterministic. This ensures the build system
            // only detects a change when the preserved members are different.
            foreach (var assemblyMembers in m_MembersToPreserve.OrderBy(a => a.Key.GetName().Name))
            {
                linkXml.AppendLine($"  <assembly fullname=\"{assemblyMembers.Key.GetName().Name}\">");
            
                foreach (var typeMembers in assemblyMembers.Value.OrderBy(t => t.Key.FullName))
                {
                    linkXml.AppendLine($"    <type fullname=\"{FormatForXml(ToCecilName(typeMembers.Key.FullName))}\">");
            
                    foreach (var member in typeMembers.Value.OrderBy(m => m.Name))
                    {
                        var memberName = FormatForXml(member.Name);
            
                        switch (member)
                        {
                            case FieldInfo field:
                            {
                                linkXml.AppendLine($"      <field name=\"{memberName}\" />");
                                break;
                            }
                            case PropertyInfo property:
                            {
                                linkXml.AppendLine($"      <property name=\"{memberName}\" />");
                                break;
                            }
                            case MethodInfo method:
                            {
                                linkXml.AppendLine($"      <method name=\"{memberName}\" />");
                                break;
                            }
                        }
                    }
            
                    linkXml.AppendLine("    </type>");
                }
            
                linkXml.AppendLine("  </assembly>");
            }
            
            linkXml.AppendLine("</linker>");
            
            return linkXml.ToString();
        }

        static string ToCecilName(string fullTypeName)
        {
            return fullTypeName.Replace('+', '/');
        }

        static string FormatForXml(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
