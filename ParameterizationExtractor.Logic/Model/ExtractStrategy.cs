using Quipu.ParameterizationExtractor.Logic.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Quipu.ParameterizationExtractor.Logic.Model
{
    [Serializable]
    [XmlInclude(typeof(FKDependencyExtractStrategy))]
    [XmlInclude(typeof(OnlyParentExtractStrategy))]
    [XmlInclude(typeof(OnlyChildrenExtractStrategy))]
    [XmlInclude(typeof(OnlyOneTableExtractStrategy))]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind",
                     IgnoreUnrecognizedTypeDiscriminators = false,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(FKDependencyExtractStrategy), "FKDependency")]
    [JsonDerivedType(typeof(OnlyOneTableExtractStrategy), "OnlyOneTable")]
    [JsonDerivedType(typeof(OnlyChildrenExtractStrategy), "OnlyChildren")]
    [JsonDerivedType(typeof(OnlyParentExtractStrategy),  "OnlyParent")]
    public class ExtractStrategy : IAmDSLFriendly
    {
        public ExtractStrategy()
        {
            ProcessChildren = true;
            ProcessParents = true;
        }
        protected ExtractStrategy(bool processChildren, bool processParents) : this(processChildren, processParents, new List<string>())
        {
        }


        protected ExtractStrategy(bool processChildren, bool processParents, List<string> dependencyToExclue)
        {
            ProcessChildren = processChildren;
            ProcessParents = processParents;
            DependencyToExclude = dependencyToExclue;
        }
        [XmlAttribute()]
        public bool ProcessChildren { get; set; }
        [XmlAttribute()]
        public bool ProcessParents { get; set; }
        [XmlAttribute()]
        public string Where { get; set; }        
        public List<string> DependencyToExclude { get; set; }

        public virtual string AsString() { return string.Empty; } 
    }

    public class FKDependencyExtractStrategy: ExtractStrategy, IAmDSLFriendly
    {
        public FKDependencyExtractStrategy() : base(true, true) { }
        public FKDependencyExtractStrategy(List<string> dependencyToExclue) : base(true, true, dependencyToExclue) { }

        public override string AsString() => "FK";
    }

    public class OnlyParentExtractStrategy : ExtractStrategy
    {
        public OnlyParentExtractStrategy() : base(false, true) { }
        public override string AsString() => "Parents";
    }

    public class OnlyChildrenExtractStrategy : ExtractStrategy
    {
        public OnlyChildrenExtractStrategy() : base(true, false) { }
        public override string AsString() => "Children";
    }

    public class OnlyOneTableExtractStrategy : ExtractStrategy
    {
        public OnlyOneTableExtractStrategy() : base(false, false) { }
        public override string AsString() => "OneTable";
    }
}
