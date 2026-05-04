using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Configs;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Quipu.ParameterizationExtractor.Configs
{
    public class ConfigSerializer : ICanSerializeConfigs
    {
        private const string _pathToGlobalConfig = @"ExtractConfig.xml";
        private readonly IDSLConnector _dslConnector;

        public ConfigSerializer(IDSLConnector dslConnector)
        {
            Affirm.IsNotNull(dslConnector, "dslConnector");

            _dslConnector = dslConnector; 
        }
        public IExtractConfiguration GetGlobalConfig()
        {
            var serializer = new XmlSerializer(typeof(GlobalExtractConfiguration));

            using (var reader = new StreamReader(_pathToGlobalConfig))
            {
                return (IExtractConfiguration)serializer.Deserialize(reader);
            }

        }

        public IPackage GetPackage(string path)
        {
            var fi = new FileInfo(path);

            if (!fi.Exists)
                throw new FileNotFoundException(path);

            if (fi.Extension == ".xml")
            {
                var serializer = new XmlSerializer(typeof(Package));

                using (var reader = new StreamReader(path))
                {
                    return (IPackage)serializer.Deserialize(reader);
                }
            }
            else if (fi.Extension == ".bc")
            {
                var text = File.ReadAllText(path);

                return _dslConnector.Parse(text);
            }
            else if (fi.Extension == ".json")
            {
                return JsonPackageReader.Read(path);
            }

            throw new NotSupportedException("files '*{0}' are not suported! ".FormIt(fi.Extension));
        }

        public string SerializePackage(IPackage pkg)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Package));

            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, pkg);
                return writer.ToString();
            }

        }
    }
}
