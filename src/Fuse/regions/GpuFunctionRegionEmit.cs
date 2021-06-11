using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using VL.Lang;
using VL.Lang.Platforms.Roslyn;
using VL.Lang.Symbols;
using VL.Model;

namespace Fuse.regions
{
    /*
    partial class  GpuFunctionRegion : IEmittingPlugin
    {
        public void Emit(IRoslynPatchCompiler compiler, IPatchSymbol patch, INodeSymbol node, List<SyntaxNode> syntaxNodes, IBorderControlPointSymbol bcp)
        {
            var slot = node.Slots[0];
            if (slot.IsUnused)
                return;

            if (bcp != null)
            {
                BindOutputControlPoints(compiler, patch, node, syntaxNodes, slot);
                return;
            }

            var generator = compiler.Generator;
            var masterPatch = node.Patches.Single();
            var subPatches = masterPatch.Patches;
            var thenPatch = subPatches.FirstOrDefault(p => p.Name == NameAndVersion.Then);

            var managerName = compiler.GenerateLocalName("manager");
            // The following null check could be avoided if we would emit the Create operation as non-static which we should do anyways to enable the This node.
            var managerType = slot.Type as IConcreteTypeSymbol;

            syntaxNodes.Add(generator.LocalDeclarationStatement(managerName, compiler.BoundExpressions.ValueOrDefault(slot) ?? compiler.Default(managerType)));
            var manager = generator.IdentifierName(managerName);

            var inputsName = compiler.GenerateLocalName("inputs");
            syntaxNodes.Add(generator.LocalDeclarationStatement(inputsName, 
                GetTupleInitializer(node.InputControlPoints.Where(cp => !cp.IsUnused).Select(cp => compiler.GetExpression(cp) ?? compiler.Default(cp.OuterType)))));
            var inputs = generator.IdentifierName(inputsName);

            var outputsName = compiler.GenerateLocalName("outputs");
            syntaxNodes.Add(generator.LocalDeclarationStatement(outputsName, 
                generator.MemberAccessExpression(manager, nameof(Manager<ValueTuple, object, ValueTuple>.Outputs))));
            var outputs = generator.IdentifierName(outputsName);

            var forceInput = node.Inputs[0];
            var changedOutput = node.Outputs[0];

            var updateName = nameof(Manager<ValueTuple, object, ValueTuple>.Update);

            var changedOutName = compiler.GenerateLocalName(changedOutput);
            syntaxNodes.Add(generator.LocalDeclarationStatement(changedOutName,
                generator.LogicalOrExpression(
                left: compiler.GetExpression(forceInput),
                right: generator.InvocationExpression(generator.MemberAccessExpression(manager, nameof(Manager<ValueTuple, object, ValueTuple>.InputsChanged)), inputs))));
            var hasChanged = generator.IdentifierName(changedOutName);
            compiler.Bind(changedOutput, hasChanged);

            using var trueStatements = Pooled.GetList<SyntaxNode>();
            using var falseStatements = Pooled.GetList<SyntaxNode>();

            var stateName = compiler.GenerateLocalName("state");
            trueStatements.Add(generator.LocalDeclarationStatement(stateName, 
                generator.MemberAccessExpression(manager, "State")));
            var state = generator.IdentifierName(stateName);
            var stateType = node.PrivateHelperType;
            
            // manager.BeginInputs();
            trueStatements.Add(generator.InvocationExpression(generator.MemberAccessExpression(manager,
                nameof(Manager<ValueTuple, object, ValueTuple>.BeginInputs)))
            );
            foreach (var cp in node.InputControlPoints)
            {
                var parameterName = compiler.GenerateLocalName("parameter");
                trueStatements.Add(generator.LocalDeclarationStatement(parameterName, 
                    generator.InvocationExpression(generator.MemberAccessExpression(manager,
                        nameof(Manager<ValueTuple, object, ValueTuple>.CreateInput)),compiler.GetExpression(cp))
                ));
                    
                compiler.Bind(cp, generator.IdentifierName(parameterName));
            }
            // manager.EndInputs();
            trueStatements.Add(generator.InvocationExpression(generator.MemberAccessExpression(manager,
                nameof(Manager<ValueTuple, object, ValueTuple>.EndInputs)))
            );
            
            using (var bindings = compiler.SaveBindings())
            {
                // state = CREATE
                var createStatements = Pooled.GetList<SyntaxNode>();
                var createPatch = subPatches.FirstOrDefault(p => p.Name == NameAndVersion.Create);
                if (createPatch != null)
                    compiler.Compile(createStatements.Value, createPatch, null);
                var fieldValues = compiler.GetFieldWrites(stateType);
                createStatements.Add(
                    generator.AssignmentStatement(state, compiler.CreateValue(stateType, fieldValues)));

                // if (state == null)
                trueStatements.Add(
                    generator.IfStatement(
                        condition: generator.ReferenceEqualsExpression(state, generator.NullLiteralExpression()),
                        trueStatements: createStatements.Value));

                createStatements.Free();
            }

            // Fetch statements for the then part of the cache region
            compiler.Compile(trueStatements.Value, thenPatch, state);

            // Update the state
            // state = state.With(...)
            var fieldWrites = compiler.GetFieldWrites(stateType);
            if (fieldWrites.Count > 0)
            {
                trueStatements.Add(
                    generator.AssignmentStatement(
                        left: state,
                        right: compiler.UpdateValue(stateType, state, fieldWrites)));
            }

            // Update the outputs
            // outputs = (x, y, t);
            trueStatements.Add(
                generator.AssignmentStatement(
                    left: outputs,
                    right: GetTupleInitializer(node.OutputControlPoints.Where(cp => !cp.OuterType.IsUnused).Select(cp => compiler.GetExpression(cp) ?? compiler.Default(cp.OuterType)))));

            // Update the manager
            trueStatements.Value.Add(
                generator.AssignmentStatement(
                    left: manager, 
                    right: generator.InvocationExpression(
                        generator.MemberAccessExpression(manager, updateName), inputs, state, outputs)));

            falseStatements.Value.Add(
                generator.InvocationExpression(
                    generator.MemberAccessExpression(manager, updateName), inputs));

            syntaxNodes.Add(generator.IfStatement(hasChanged, trueStatements.Value, falseStatements.Value));

            // Deconstruct the tuple and bind the outputs
            BindOutputControlPoints(compiler, patch, node, syntaxNodes, outputs);

            // Ensure the manager gets written back
            compiler.SlotAssignments[slot] = manager;

            SyntaxNode GetTupleInitializer(IEnumerable<SyntaxNode> arguments)
            {
                using var args = arguments.ToPooledList();
                switch (args.Value.Count)
                {
                    case 0:
                        return generator.InvocationExpression(
                            generator.MemberAccessExpression(compiler.GetTypeExpression(typeof(ValueTuple)), nameof(ValueTuple.Create)));
                    case 1:
                        return generator.InvocationExpression(
                            generator.MemberAccessExpression(compiler.GetTypeExpression(typeof(ValueTuple)), nameof(ValueTuple.Create)),
                            args.Value[0]);
                    default:
                        return generator.TupleExpression(args.Value);
                }
            }
        }

        private static void BindOutputControlPoints(IRoslynPatchCompiler compiler, IPatchSymbol patch, INodeSymbol node, List<SyntaxNode> syntaxNodes, INamedSymbol slot)
        {
            // Can be null in case the output is connected on Create while the region itself is on Update.
            // In that case the Cache region never had a chance to run so we can only return the default value.
            // Seen in VL.Xenko.Games/GetScopedVariable
            if (compiler.Instance is null)
                return;

            var generator = compiler.Generator;
            var outputs = generator.MemberAccessExpression(
                generator.MemberAccessExpression(compiler.Instance, slot.MetadataName),
                nameof(Manager<ValueTuple, object, ValueTuple>.Outputs));
            BindOutputControlPoints(compiler, patch, node, syntaxNodes, outputs);
        }

        private static void BindOutputControlPoints(IRoslynPatchCompiler compiler, IPatchSymbol patch, INodeSymbol node, List<SyntaxNode> syntaxNodes, SyntaxNode outputs)
        {
            // var (x, y, ...) = outputs
            using var outputNames = Pooled.GetList<string>();
            var generator = compiler.Generator;
            foreach (var cp in node.OutputControlPoints)
            {
                if (cp.OuterType.IsUnused)
                    continue;
                var outName = compiler.GenerateLocalName(cp.MetadataName);
                outputNames.Add(outName);
                compiler.Bind(cp, generator.IdentifierName(outName));
            }

            switch (outputNames.Value.Count)
            {
                case 0:
                    break;
                case 1:
                    syntaxNodes.Add(
                        generator.LocalDeclarationStatement(
                            name: outputNames.Value[0],
                            initializer: generator.MemberAccessExpression(outputs, nameof(ValueTuple<object>.Item1))));
                    break;
                default:
                    syntaxNodes.Add(
                        generator.AssignmentStatement(
                            left: DeclarationExpression(
                                IdentifierName("var"),
                                ParenthesizedVariableDesignation(SeparatedList<VariableDesignationSyntax>(outputNames.Value.Select(n => SingleVariableDesignation(Identifier(n)))))),
                            right: outputs));
                    break;
            }
        }

        // Sample code
        class Test
        {
            private Manager<ValueTuple, object, ValueTuple> manager;

            public Test()
            {
                manager = new Manager<ValueTuple, object, ValueTuple>(default);
            }

            void Update()
            {
                var inputs = ValueTuple.Create();
                // var inputs = ValueTuple.Create(a);
                // var inputs = (a, b, c);
                var outputs = manager.Outputs;
                var hasChanged = false || manager.InputsChanged(inputs);
                if (hasChanged)
                {
                    manager.BeginInputs();
                    manager.CreateInput(new ConstantValue<float>(0f));
                    manager.EndInputs();
                    
                    var state = manager.State ?? new object();
                    outputs = ValueTuple.Create();
                    
                    
                    
                    manager = manager.Update(inputs, state, outputs);
                }
                else
                {
                    manager = manager.Update(inputs);
                }
                // var x = outputs.Item1;
                // var (x, y, z) = outputs;
            }

            void Dispose()
            {
                manager.Dispose();
            }
        }

        /// <summary>
        /// var manager = this.manager;
        /// var inputs = (a, b, c);
        /// if (force || manager.InputsChanged(inputs))
        /// {
        ///   var state = manager.State;
        ///   if (state is null)
        ///     state = CREATE
        ///   UPDATE
        ///   var outputs = (x, y, z);
        ///   manager = manager.Update(inputs, disposeOutputs, state, outputs);
        /// }
        /// else
        /// {
        ///   manager = manager.Update(inputs, disposeOutputs);
        /// }
        /// </summary>
        /// <typeparam name="TInputs"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <typeparam name="TOutputs"></typeparam>
        public class Manager<TInputs, TState, TOutputs> : IDisposable
            where TInputs : struct, ITuple, IEquatable<TInputs>
            where TOutputs : struct, ITuple, IEquatable<TOutputs>
            where TState : class
        {
            TInputs inputs;
            TState state;
            TOutputs outputs;

            public Manager(TOutputs outputs) 
            {
                this.outputs = outputs;
            }

            private Manager(TInputs inputs, TState state, TOutputs outputs)
            {
                this.inputs = inputs;
                this.state = state;
                this.outputs = outputs;
            }

            public TState State => state;

            public TOutputs Outputs => outputs;

            public bool InputsChanged(TInputs inputs = default)
            {
                return !this.inputs.Equals(inputs);
            }

            public void BeginInputs()
            {
                
            }
            
            public GpuValue<T> CreateInput<T>(GpuValue<T> theValue)
            {
                return new PatchedFunctionParameter<T>(new ConstantValue<T>(default(T))).Output;
            }
            
            public void EndInputs()
            {
                
            }

            // Called if cache wasn't triggered - last inputs and whether or not to dispose needs to be remembered
            public Manager<TInputs, TState, TOutputs> Update(TInputs inputs)
            {
                return With(inputs, state, outputs);
            }

            // Called if cache was triggered
            public Manager<TInputs, TState, TOutputs> Update(TInputs inputs, TState state, TOutputs outputs)
            {
                return With(inputs, state, outputs);
            }

            public Manager<TInputs, TState, TOutputs> With(TInputs inputs, TState state, TOutputs outputs)
            {
                
                    this.inputs = inputs;
                    this.state = state;
                    this.outputs = outputs;
                    return this;
            }

            public void Dispose()
            {
                Dispose(state);
            }

            static void Dispose(object obj)
            {
                if (obj is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }*/
}