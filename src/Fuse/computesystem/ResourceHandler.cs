using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.ComputeSystem
{
    public interface IResourceHandler
    {
        public AbstractResource CreateResource(string name);

        public void GetThreadGroupInfo(out Int3 threadGroupCount, out Int3 threadGroupSize);

        public AbstractResourceBinder CreateBinder(AbstractResource resource, ShaderNode<Int3> index);
    }

    public interface IBufferCreator
    {
        public Buffer CreateBuffer(int elementCount, int stride);
    }
    
    public class BufferResourceHandler : IResourceHandler
    {
        private Dictionary<string, BufferResource> _resources;

        private readonly IBufferCreator _bufferCreator;

        public int ThreadGroupSize { get; set; }

        public int ElementCount { get; set; }

        private NodeContext _context;

        private int _subContextId;

        public BufferResourceHandler(NodeContext nodeContext, IBufferCreator theBufferCreator)
        {
            _context = nodeContext;
            _bufferCreator = theBufferCreator;
            ThreadGroupSize = 128;
            ElementCount = 1000000;
            _resources = new Dictionary<string, BufferResource>();
        }

        public AbstractResource CreateResource(string theName)
        {
            return new BufferResource(_context, theName, ElementCount, _bufferCreator);
        }

        public void GetThreadGroupInfo(out Int3 threadGroupCount, out Int3 threadGroupSize)
        {
            var _threadGroupCount = (ElementCount + ThreadGroupSize - 1) / ThreadGroupSize;
            _threadGroupCount = Math.Max(1, _threadGroupCount);
            threadGroupCount = new Int3(_threadGroupCount,1,1);
            threadGroupSize = new Int3(ThreadGroupSize,1,1);
        }

        public AbstractResourceBinder CreateBinder(AbstractResource theResource, ShaderNode<Int3> theIndex)
        {
            var getMember = new GetMember<Int3, int>(ShaderNodesUtil.GetContext(_context,_subContextId++));
            getMember.SetInput("x", theIndex);
            return new BufferResourceBinder(_context,(BufferResource)theResource, getMember );
        }
    }
}