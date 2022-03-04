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
            
            var compare = new OperatorNode<float, bool>(gpuValue0.Output, gpuValue1.Output,new ConstantValue<bool>(true),">");
            Console.WriteLine(compare.BuildSourceCode());
            
            var compareNull = new OperatorNode<float, bool>(gpuValue0.Output, null,new ConstantValue<bool>(true),">");
            Console.WriteLine(compareNull.BuildSourceCode());
        }
        
        [Test]
        public static void TestBooleanSwitch()
        {
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var compare = new OperatorNode<float, bool>(gpuValue0.Output, null,new ConstantValue<bool>(true),">");
            
            var switchVal = new BooleanSwitchNode<float>(compare.Output, gpuValue0.Output, gpuValue1.Output,new ConstantValue<float>(0));
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new BooleanSwitchNode<float>(compare.Output, gpuValue0.Output, null,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new BooleanSwitchNode<float>(compare.Output, null, gpuValue1.Output,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new BooleanSwitchNode<float>(compare.Output, null, null,new ConstantValue<float>(0));
            Console.WriteLine(switchValNull3.BuildSourceCode());
        }
        
        [Test]
        public static void TestBooleanSwitchVoid()
        {
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();

            var val0 = new DeclareValue<float>(gpuValue0.Output);
            var val1 = new DeclareValue<float>(gpuValue1.Output);

            var assign0 = new AssignNode<float>(val0.Output, gpuValue0.Output);
            var assign1 = new AssignNode<float>(val1.Output, gpuValue1.Output);
            
            var compare = new OperatorNode<float, bool>(gpuValue0.Output, null,new ConstantValue<bool>(true),">");
            
            var switchVal = new BooleanSwitchNode<GpuVoid>(compare.Output, assign0.Output, assign1.Output);
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new BooleanSwitchNode<GpuVoid>(compare.Output, assign0.Output, null);
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new BooleanSwitchNode<GpuVoid>(compare.Output, null, assign1.Output);
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new BooleanSwitchNode<GpuVoid>(compare.Output, null, null);
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
            
            var compare = new OperatorNode<float, bool>(gpuValue0.Output, gpuValue1.Output,new ConstantValue<bool>(false),">");
            Console.WriteLine(compare.BuildSourceCode());
            
            var switchVal = new NumericSwitchNode<float>(gpuValueCheck.Output, new List<GpuValue<float>>(){gpuValue0.Output, gpuValue1.Output, gpuValue2.Output}, gpuValue3.Output);
            Console.WriteLine(switchVal.BuildSourceCode());
        }
    }
}