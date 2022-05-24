using System;
using System.Collections.Generic;
using Fuse;
using Fuse.compute;
using NUnit.Framework;

namespace PatchTests
{
    public static class TestControl
    {
        [Test]
        public static void TestBooleanOperator()
        {
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var compare = new OperatorNode<float, bool>(gpuValue0, gpuValue1,new ConstantValue<bool>(true),">");
            Console.WriteLine(compare.BuildSourceCode());
            
            var compareNull = new OperatorNode<float, bool>(gpuValue0, null,new ConstantValue<bool>(true),">");
            Console.WriteLine(compareNull.BuildSourceCode());
        }
        
        [Test]
        public static void TestBooleanSwitch()
        {
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var compare = new OperatorNode<float, bool>(gpuValue0, null,new ConstantValue<bool>(true),">");
            
            var switchVal = new IfNodeDeprecated<float>(compare, gpuValue0, gpuValue1,new ConstantValue<float>(0));
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new IfNodeDeprecated<float>(compare, gpuValue0, null,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new IfNodeDeprecated<float>(compare, null, gpuValue1,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new IfNodeDeprecated<float>(compare, null, null,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull3.BuildSourceCode());
        }
        
        [Test]
        public static void TestBooleanSwitchVoid()
        {
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();

            var val0 = new DeclareValue<float>(gpuValue0);
            var val1 = new DeclareValue<float>(gpuValue1);

            var assign0 = new AssignNode<float>(val0, gpuValue0);
            var assign1 = new AssignNode<float>(val1, gpuValue1);
            
            var compare = new OperatorNode<float, bool>(gpuValue0, null,new ConstantValue<bool>(true),">");
            
            var switchVal = new IfNodeDeprecated<GpuVoid>(compare, assign0, assign1);
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new IfNodeDeprecated<GpuVoid>(compare, assign0, null);
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new IfNodeDeprecated<GpuVoid>(compare, null, assign1);
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new IfNodeDeprecated<GpuVoid>(compare, null, null);
            Console.WriteLine(switchValNull3.BuildSourceCode());
        }
        
        [Test]
        public static void TestNumericSwitch()
        {    
            var gpuValueCheck = new GpuInput<int>();
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            var gpuValue2 = new GpuInput<float>();
            var gpuValue3 = new GpuInput<float>();
            
            var compare = new OperatorNode<float, bool>(gpuValue0, gpuValue1,new ConstantValue<bool>(false),">");
            Console.WriteLine(compare.BuildSourceCode());
            
            var switchVal = new SwitchNumeric<float>(gpuValueCheck, new List<ShaderNode<float>>(){gpuValue0, gpuValue1, gpuValue2}, gpuValue3);
            Console.WriteLine(switchVal.BuildSourceCode());
        }
    }
}