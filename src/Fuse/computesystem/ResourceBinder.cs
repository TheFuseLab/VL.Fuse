using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.ComputeSystem
{
    public abstract class AbstractResourceBinder
    {
        
        protected readonly Dictionary<string, AbstractShaderNode> Attributes;

        protected AbstractResourceBinder()
        {
            Attributes = new Dictionary<string, AbstractShaderNode>();
        }
        public abstract ShaderNode<GpuVoid> WriteAttributes();

        public AbstractShaderNode GetAttribute(string theName)
        {
            return Attributes[theName];
        }

        public abstract ShaderNode<GpuVoid> WriteToIndex(ShaderNode<int> theIndex);

        public abstract void UpdateResource(AbstractResource theResource);
    }

    public class BufferResourceBinder : AbstractResourceBinder
    {
        private readonly BufferResource _resource;

        private readonly BufferInput<GpuStruct> _input;
        private readonly BufferGet<GpuStruct> _bufferGet;
        private readonly ShaderNode<int> _index;

        private readonly Dictionary<string, AbstractShaderNode> _bufferedAttributes;
        
        public BufferResourceBinder(BufferResource theResource, ShaderNode<int> theIndex)
        {
            _resource = theResource;
            _index = theIndex;
            
            _input = new BufferInput<GpuStruct>(theResource.Struct, theResource.Buffer);
            _bufferGet = new BufferGet<GpuStruct>(_input, theIndex, _resource.Struct);
            _bufferGet.AddInput(_resource.Struct);

            _bufferedAttributes = new Dictionary<string, AbstractShaderNode>();
            
            theResource.Attributes.Values.ForEach(attribute =>
            {
                var declareValue = AbstractCreation.AbstractDeclareValue(attribute.ShaderNode);
                var getMember = AbstractCreation.AbstractGetMember(_bufferGet, attribute.ShaderNode);

                Attributes[attribute.Name] = attribute.IsBuffered ? getMember : declareValue;

                if (attribute.IsBuffered) _bufferedAttributes[attribute.Name] = getMember;
            });
        }

        public override ShaderNode<GpuVoid> WriteAttributes()
        {
            return WriteToIndex(_index);
        }

        public override ShaderNode<GpuVoid> WriteToIndex(ShaderNode<int> theIndex)
        {
            var nodes = new List<ShaderNode<GpuVoid>>();
            foreach (var kv in _bufferedAttributes)
            {
                nodes.Add(new AssignValueToMember<GpuStruct>(_bufferGet, kv.Key, kv.Value));
            }
            nodes.Add(new BufferSet<GpuStruct>(_input, theIndex, _bufferGet));

            return new Group(nodes);
        }

        public override void UpdateResource(AbstractResource theResource)
        {
            if (_input != null) _input.Value = (theResource as BufferResource)?.Buffer;
        }

        public BufferGet<GpuStruct> GetElement(ShaderNode<int> theIndex)
        {
            return new BufferGet<GpuStruct>(_input, theIndex, _resource.Struct);
        }
    }
}