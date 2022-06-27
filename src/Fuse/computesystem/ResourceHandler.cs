using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.ComputeSystem
{
    public interface IResourceHandler
    {
        public AbstractResource CreateResource(string theName);

        public void GetThreadGroupInfo(out Int3 theThreadGroupCount, out Int3 theThreadGroupSize);

        public AbstractResourceBinder CreateBinder(AbstractResource theResource, ShaderNode<Int3> theIndex);

        public int Ticket { get; }
    }

    public interface IBufferCreator
    {
        public Buffer CreateBuffer(int theElementCount, int theStride);
    }
    
    public class BufferResourceHandler : IResourceHandler
    {
        private int _ticket;

        private int _elementCount;

        private Dictionary<string, AbstractResource> _resources;

        private IBufferCreator _bufferCreator;

        public BufferResourceHandler(IBufferCreator theBufferCreator)
        {
            _bufferCreator = theBufferCreator;
            _ticket = 0;
            ThreadGroupSize = 128;
            _elementCount = 1000000;
            _resources = new Dictionary<string, AbstractResource>();
        }

        public int ThreadGroupSize { get; set; }

        public AbstractResource CreateResource(string theName)
        {
            return new BufferResource(theName, _elementCount);
        }

        public void GetThreadGroupInfo(out Int3 theThreadGroupCount, out Int3 theThreadGroupSize)
        {
            var threadGroupCount = (ElementCount + ThreadGroupSize - 1) / ThreadGroupSize;
            threadGroupCount = Math.Max(1, threadGroupCount);
            theThreadGroupCount = new Int3(threadGroupCount,1,1);
            theThreadGroupSize = new Int3(ThreadGroupSize,1,1);
        }

        public AbstractResourceBinder CreateBinder(AbstractResource theResource, ShaderNode<Int3> theIndex)
        {
            return new BufferResourceBinder((BufferResource)theResource, new GetMember<Int3,int>(theIndex, "x") );
        }

        public int ElementCount
        {
            get => _elementCount;
            set
            {
                if (value != _elementCount)
                {
                }

                _elementCount = value;
            }
        }

        public int Ticket => _ticket;


    }
}