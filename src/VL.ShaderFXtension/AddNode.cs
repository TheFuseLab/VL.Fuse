﻿using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class AddNode<T> :OperatorNode<T,T>
    {
        public AddNode(IEnumerable<GpuValue<T>> inputs) : base(inputs,new ConstantValue<T>(0), "+")
        {
        }
    }
}