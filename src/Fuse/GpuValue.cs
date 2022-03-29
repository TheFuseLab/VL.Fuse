using System;
using Stride.Engine;
using VL.Core;
using VL.UI.Core;
namespace Fuse
{
    public abstract class AbstractGpuValue
    {
        public  string Name{ get; set; }
        
        public Entity Helper{ get; set; }

        public object ToolTip { get; set; }

        public bool HelperEnabled{ get; set; }

        public int HashCode;

        protected AbstractGpuValue(string theName)
        {
            Name = theName;
            HashCode = -1;
        }

        public abstract string ID { get; }

        public abstract string TypeName(); 

        public AbstractShaderNode ParentNode { get; set; }

        public abstract Type DataType { get; }
    }

    [Monadic(typeof(GpuValueMonadicFactory<>))]
    public class GpuValue<T> : AbstractGpuValue
    {

        public string TypeOverride { get; set; }

        public GpuValue(string theName) : base(theName)
        {
        }

        public override string TypeName()
        {
            if (typeof(T) == typeof(GpuStruct))
            {
                return TypeOverride;
            }
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public override string ID
        {
            get
            {
                
                return Name + "_" + HashCode;
            }
        }

        public override Type DataType => typeof(T);

    }

    public class GpuValuePassThrough<T> : GpuValue<T>
    {
       
        private GpuValue<T> _value;
        public GpuValuePassThrough(GpuValue<T> theValue) : base(theValue == null ? "": theValue.Name)
        {
            _value = theValue ?? new GpuValue<T>("");
        }

        public void SetInput(GpuValue<T> theValue)
        {
            
            _value = theValue ?? new GpuValue<T>("");
        }
        public override string ID => _value.ID;
    }

    public class GpuNumericValue<T> : GpuValue<T> where T : struct
    {
        public GpuNumericValue(string theName) : base(theName)
        {
        }
    }
}