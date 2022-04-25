using System;
using Stride.Engine;
using VL.Core;
using VL.UI.Core;
namespace Fuse
{
    public abstract class AbstractGpuValue2
    {
        public  string Name{ get; set; }
        
        public Entity Helper{ get; set; }

        public object ToolTip { get; set; }

        public bool HelperEnabled{ get; set; }

        public int HashCode;

        protected AbstractGpuValue2(string theName)
        {
            Name = theName;
            HashCode = -1;
        }

        public abstract string ID { get; }

        public abstract string TypeName(); 

        public AbstractShaderNode ParentNode { get; set; }

        public abstract Type DataType { get; }
    }

    
    public class GpuValue2<T> : AbstractGpuValue2
    {

        public string TypeOverride { get; set; }

        public GpuValue2(string theName) : base(theName)
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

    public class GpuValuePassThrough2<T> : GpuValue2<T>
    {
       
        private GpuValue2<T> _value;
        public GpuValuePassThrough2(GpuValue2<T> theValue) : base(theValue == null ? "": theValue.Name)
        {
            _value = theValue ?? new GpuValue2<T>("");
        }

        public void SetInput(GpuValue2<T> theValue)
        {
            
            _value = theValue ?? new GpuValue2<T>("");
        }
        public override string ID => _value.ID;
    }

    public class GpuNumericValue<T> : GpuValue2<T> where T : struct
    {
        public GpuNumericValue(string theName) : base(theName)
        {
        }
    }
}