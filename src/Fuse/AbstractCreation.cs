using System;
using Fuse.compute;
using Fuse.function;
using Stride.Graphics;

namespace Fuse
{
    public static class AbstractCreation
    {
        public static AbstractShaderNode CreateAbstract(AbstractShaderNode theValue, Type theBaseType, object[] theArguments )
        {
            var nodeType = GetBaseType(theValue);
            
            var dataType = new[] { nodeType.GetGenericArguments()[0]};
            var getType = theBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theArguments) as AbstractShaderNode;
        }

        private static Type GetBaseType(AbstractShaderNode theNode)
        {
            var nodeType = theNode.GetType();
            while (nodeType is {BaseType: { }} && nodeType.BaseType != typeof(AbstractShaderNode))
            {
                nodeType = nodeType.BaseType;
            }

            return nodeType;
        }
        
        public static AbstractShaderNode AbstractGetMember<T>(NodeSubContextFactory theSubContextFactory, ShaderNode<T> theStruct, AbstractShaderNode theMember)
        {
            var getMemberBaseType = typeof(GetMember<,>);
            var nodeType = GetBaseType(theMember);
            var dataType = new[] {typeof(T), nodeType.GetGenericArguments()[0]};
            var getType = getMemberBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember.Name, null) as AbstractShaderNode;
          
        }
        
        public static AbstractShaderNode AbstractSetMember<T>(NodeSubContextFactory theSubContextFactory, ShaderNode<T> theStruct, string theMember, AbstractShaderNode theValue)
        {
            //return CreateAbstract(theMember, typeof(AssignValueToMember<>), new object[]{theSubContextFactory.NextSubContext(), theStruct, theMember.Name, theValue});
            
            
            var setMemberBaseType = typeof(SetMember<>);
            var dataType = new[] {typeof(T)};
            var getType = setMemberBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember, theValue) as AbstractShaderNode;
           
        }
        
        public static AbstractShaderNode AbstractFunctionParameter(NodeSubContextFactory theSubContextFactory, Type theParameterType, InputModifier theModifier = InputModifier.In, int theId = 0 )
        {
            var baseType = typeof(FunctionParameter<>);
            var dataType = new[] { theParameterType.GetGenericArguments()[0]};
            var getType = baseType.MakeGenericType(dataType);
            
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), null, theModifier, theId) as AbstractShaderNode;
           
        }
        
        public static AbstractShaderNode AbstractFunctionParameter(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theParameter, InputModifier theModifier = InputModifier.In, int theId = 0 )
        {
            return AbstractFunctionParameter(theSubContextFactory, GetBaseType(theParameter),theModifier, theId);
        }
        
        public static AbstractShaderNode AbstractComputeTextureGet(NodeSubContextFactory theSubContextFactory, ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            
            var indexType = GetBaseType(theIndex);
            var valueType = GetBaseType(theValue);
            
            var baseType = typeof(ComputeTextureGet<,>);
            var dataType = new[] {indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0]};
            var getType = baseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, null) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractComputeTextureSet(NodeSubContextFactory theSubContextFactory, ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            
            var indexType = GetBaseType(theIndex);
            var valueType = GetBaseType(theValue);
            
            var baseType = typeof(ComputeTextureSet<,>);
            var dataType = new[] {indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0]};
            var getType = baseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, theValue) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractShaderNodePassThrough(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(PassThroughNode<>), new object[]{theSubContextFactory.NextSubContext(), "pass", theValue});
        }
        
        public static AbstractShaderNode AbstractDeclareValue(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[]{theSubContextFactory.NextSubContext(), null});
        }
        
        public static AbstractShaderNode AbstractDeclareValueAssigned(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[] {theSubContextFactory.NextSubContext(), theValue});
        }
        
        public static AbstractShaderNode AbstractOutput(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theComputation, AbstractShaderNode theOutput)
        {
            var result = CreateAbstract(theOutput, typeof(Output<>), new object[] {theSubContextFactory.NextSubContext(),theComputation, theOutput, null});
            return result;
        }
        
        public static AbstractShaderNode AbstractAssignNode(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theTarget, AbstractShaderNode theSource)
        {
            return CreateAbstract(theTarget, typeof(AssignValue<>), new object[] {theSubContextFactory.NextSubContext(), theTarget, theSource});
        }
        
        public static AbstractShaderNode AbstractConstant(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theGpuValue, float theValue)
        {
            return AbstractConstant(theGpuValue.GetType().BaseType,theValue);
        }
        
        public static AbstractShaderNode AbstractConstant(Type theType, float theValue)
        {
            var dataType = new[] { theType.GetGenericArguments()[0]};
            return ConstantHelper.AbstractFromFloat( dataType[0], theValue);
        }
        
        public static AbstractShaderNode AbstractSemantic(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue,string theName, bool define = false, string theSemantic = null)
        {
            return CreateAbstract(theValue, typeof(Semantic<>), new object[]{theSubContextFactory.NextSubContext(), theName, define, theSemantic});
        }
    }
}