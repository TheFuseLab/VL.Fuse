using System.Collections.Generic;
using VL.Lang.Platforms.Roslyn;
using VL.Lang.Symbols;
using Microsoft.CodeAnalysis;

namespace Fuse.regions
{
    partial class ShaderFunctionRegion : IEmittingPlugin
    {
        class EmitExample
        {
            void Update()
            {
                // Upstream of our region some part of our graph gets computed
                Vertex<float> x;
                Vertex<float> y;

                // Here follows the code we need to generate
                // What do we want to do?                
            }
        }

        public void Emit(IRoslynPatchCompiler compiler, IPatchSymbol patch, INodeSymbol node, List<SyntaxNode> syntaxNodes, IBorderControlPointSymbol cp = null)
        {
            //// Example of how the if region looks like

            //var generator = compiler.Generator;
            //var masterPatch = node.Patches.Single();
            //var subPatches = masterPatch.Patches;
            //var thenPatch = subPatches.FirstOrDefault(p => p.Name == NameAndVersion.Then);
            //var state = default(SyntaxNode);

            //// Fetch our manager
            //var stateInput = node.GetStateInput();
            //if (stateInput != null && stateInput.IsConnected)
            //{
            //    var stateLocal = compiler.GenerateLocalName("state");
            //    state = generator.IdentifierName(stateLocal);
            //    syntaxNodes.Add(generator.LocalDeclarationStatement(stateLocal, compiler.GetExpression(stateInput)));

            //    var stateOutput = node.GetStateOutput();
            //    if (stateOutput != null)
            //        compiler.Bind(stateOutput, state);
            //}

            //// if (CONDITION)
            //var condition = default(SyntaxNode);
            //var conditionInput = node.Inputs.FirstOrDefault(p => p.Name == Names.ConditionInput);
            //if (conditionInput != null)
            //    condition = compiler.GetExpression(conditionInput);
            //else
            //    condition = generator.FalseLiteralExpression();

            //// Declare outputs
            //var outputLocals = Pooled.GetDictionary<IBorderControlPointSymbol, SyntaxNode>();
            //foreach (var outputCp in node.OutputControlPoints.Where(p => p.IsAccumulator && !p.IsUnused))
            //{
            //    // The output control point could be directly connected from outside, so check first whether a local was already created for it
            //    if (compiler.Locals.TryGetValue(outputCp, out var local))
            //        outputLocals.Add(outputCp, local.Identifier);
            //    else
            //    {
            //        var localType = compiler.GetTypeExpression(outputCp.OuterType);
            //        var localName = compiler.GenerateLocalName(outputCp);
            //        outputLocals.Add(outputCp, generator.IdentifierName(localName));
            //        syntaxNodes.Add(generator.LocalDeclarationStatement(localType, localName));
            //    }
            //}

            //var trueStatements = Pooled.GetList<SyntaxNode>();

            //// Compile the create part of the if
            //var stateType = node.PrivateHelperType;
            //if (state != null)
            //{
            //    using (var bindings = compiler.SaveBindings())
            //    {
            //        // state = CREATE
            //        var createStatements = Pooled.GetList<SyntaxNode>();
            //        var createPatch = subPatches.FirstOrDefault(p => p.Name == NameAndVersion.Create);
            //        if (createPatch != null)
            //            compiler.Compile(createStatements.Value, createPatch, null);
            //        var fieldValues = compiler.GetFieldWrites(stateType);
            //        createStatements.Add(
            //            generator.AssignmentStatement(state, compiler.CreateValue(stateType, fieldValues)));

            //        // if (state == null)
            //        trueStatements.Add(
            //            generator.IfStatement(
            //                condition: generator.ReferenceEqualsExpression(state, generator.NullLiteralExpression()),
            //                trueStatements: createStatements.Value));

            //        createStatements.Free();
            //    }
            //}

            //// Fetch statements for the then part of the if region
            //compiler.Compile(trueStatements.Value, thenPatch, state);

            //// Update the state
            //if (state != null)
            //{
            //    // state = state.With(...)
            //    var fieldWrites = compiler.GetFieldWrites(stateType);
            //    trueStatements.Add(
            //        generator.AssignmentStatement(
            //            left: state,
            //            right: compiler.UpdateValue(stateType, state, fieldWrites)));
            //}

            //// Write the outputs
            //var falseStatements = Pooled.GetList<SyntaxNode>();
            //foreach (var kvp in outputLocals.Value)
            //{
            //    var outputCp = kvp.Key;
            //    var output = kvp.Value;
            //    var trueValue = compiler.GetExpression(outputCp) ?? compiler.Default(outputCp.OuterType);
            //    if (trueValue.ToString() != output.ToString())
            //    {
            //        trueStatements.Add(
            //            generator.AssignmentStatement(output, trueValue));
            //    }
            //    var elseValue = default(SyntaxNode);
            //    var inputCp = outputCp.CounterPart;
            //    if (inputCp != null)
            //        elseValue = compiler.GetExpression(inputCp);
            //    if (elseValue == null)
            //        elseValue = compiler.Default(outputCp.OuterType);
            //    falseStatements.Add(generator.AssignmentStatement(output, elseValue));
            //    compiler.Bind(outputCp, output);
            //}
            //outputLocals.Free();

            //// The final if statement
            //syntaxNodes.Add(generator.IfStatement(
            //    condition: condition,
            //    trueStatements: trueStatements.Value,
            //    falseStatements: falseStatements.Value));

            //trueStatements.Free();
            //falseStatements.Free();
        }
    }
}
