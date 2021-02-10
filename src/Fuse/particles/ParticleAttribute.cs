using System;
using VL.Lib.Collections;

namespace Fuse.particles
{
   
    
    public class ParticleAttributeManager
    {
        public string Update(ParticleAttribute enumInput)
        {
            if (enumInput.IsValid())
                return enumInput.Definition.Entries[enumInput.SelectedIndex()];
            else
                return "No valid entry selected";
        }

        public ParticleAttributeDefinition GetDefinition()
        {
            return ParticleAttributeDefinition.Instance;
        }

        public void AddEnumEntry(string entry)
        {
            ParticleAttributeDefinition.Instance.AddEntry(entry, null);
        }

        public void RemoveEnumEntry(string entry)
        {
            ParticleAttributeDefinition.Instance.RemoveEntry(entry);
        }
    }

    public class ParticleAttributeDefinition : ManualDynamicEnumDefinitionBase<ParticleAttributeDefinition>
    {
        //this is optional an can be used if any initialization before the call to GetEntries is needed
        protected override void Initialize()
        {
        }

        //add this to get a node that can access the Instance from everywhere
        public new static ParticleAttributeDefinition Instance => ManualDynamicEnumDefinitionBase<ParticleAttributeDefinition>.Instance;
    }

    [Serializable]
    public class ParticleAttribute: DynamicEnumBase<ParticleAttribute, ParticleAttributeDefinition>
    {
        public ParticleAttribute(string value) : base(value)
        {
        }

        //this method needs to be imported in VL to set the default
        public static ParticleAttribute CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase();
        }
    }
}