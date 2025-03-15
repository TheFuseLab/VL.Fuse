using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;

namespace Tests.ComputeSystem
{
    // Interfaces and enums needed for testing
    public enum AttributeType
    {
        Temporary,
        StructuredBuffer,
        Texture
    }

    public interface IAttribute
    {
        string Name { get; }
        AttributeType AttributeType { get; set; }
    }

    // Simplified ShaderNode classes for testing
    public abstract class AbstractShaderNode { }
    
    public class ShaderNode<T> : AbstractShaderNode { }
    
    public class GpuVoid { }

    // AttributeMap implementation for testing
    public class AttributeMap
    {
        private HashSet<string> _attributeSet = new HashSet<string>();
        private Dictionary<string, List<IAttribute>> _attributeInstances = new Dictionary<string, List<IAttribute>>();
        private HashSet<string> _previousAttributeSet = new HashSet<string>();
        
        /// <summary>
        /// Prepares the attribute map for a new graph traversal by clearing collections
        /// and storing the current state for later comparison.
        /// </summary>
        public void Prepare()
        {
            // Store current attribute names for later comparison
            _previousAttributeSet = new HashSet<string>(_attributeSet);
            
            // Clear collections for new traversal
            _attributeSet.Clear();
            _attributeInstances.Clear();
        }
        
        /// <summary>
        /// Handles an attribute by adding it to the map.
        /// </summary>
        /// <param name="attribute">The attribute to handle.</param>
        public void HandleAttribute(IAttribute attribute)
        {
            if (attribute == null)
                return;
                
            // Add attribute name to the set
            _attributeSet.Add(attribute.Name);
            
            // Add attribute instance to the dictionary
            if (!_attributeInstances.TryGetValue(attribute.Name, out var instances))
            {
                instances = new List<IAttribute>();
                _attributeInstances[attribute.Name] = instances;
            }
            
            instances.Add(attribute);
        }
        
        /// <summary>
        /// Finishes the attribute map processing and determines if the map has changed.
        /// </summary>
        /// <returns>True if the attribute map has changed (attributes added or removed), false otherwise.</returns>
        public bool Finish()
        {
            // Check if attribute names have been added or removed
            if (_attributeSet.Count != _previousAttributeSet.Count)
                return true;
                
            // Check for any differences between current and previous attribute sets
            foreach (var attributeName in _attributeSet)
            {
                if (!_previousAttributeSet.Contains(attributeName))
                    return true;
            }
            
            foreach (var attributeName in _previousAttributeSet)
            {
                if (!_attributeSet.Contains(attributeName))
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets all attribute names currently in the map.
        /// </summary>
        /// <returns>A collection of attribute names.</returns>
        public IReadOnlyCollection<string> GetAttributeNames()
        {
            return _attributeSet;
        }
        
        /// <summary>
        /// Gets all instances of an attribute by name.
        /// </summary>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>A list of attribute instances with the specified name, or an empty list if none found.</returns>
        public IReadOnlyList<IAttribute> GetAttributeInstances(string attributeName)
        {
            if (_attributeInstances.TryGetValue(attributeName, out var instances))
                return instances.AsReadOnly();
                
            return new List<IAttribute>().AsReadOnly();
        }
    }

    /// <summary>
    /// Test class for AttributeMap
    /// </summary>
    public static class AttributeMapTests
    {
        // Mock implementation of IAttribute for testing
        private class TestAttribute : IAttribute
        {
            public TestAttribute(string name, AttributeType attributeType)
            {
                Name = name;
                AttributeType = attributeType;
            }

            public string Name { get; }
            public AttributeType AttributeType { get; set; }
            public AbstractShaderNode ShaderNode => null;
            public AbstractShaderNode InputAbstract { get; set; }
            public ShaderNode<GpuVoid> WriteCall { get; set; }
            public AbstractShaderNode ReadCall { get; set; }
            public Int3 Resolution => new Int3(1, 1, 1);
            public bool IsOverridden => false;

            public void Sync(IAttribute theAttribute) { }
        }

        public static void RunTests()
        {
            Console.WriteLine("Running AttributeMap tests...");
            
            TestPrepareAndFinish();
            TestHandleAttribute();
            TestAttributeChanges();
            
            Console.WriteLine("All AttributeMap tests completed successfully!");
        }

        private static void TestPrepareAndFinish()
        {
            Console.WriteLine("Testing Prepare and Finish methods...");
            
            var attributeMap = new AttributeMap();
            
            // Initial state should not report changes
            attributeMap.Prepare();
            bool changed = attributeMap.Finish();
            Assert(!changed, "New empty map should not report changes");
            
            // Add an attribute and verify changes
            attributeMap.Prepare();
            attributeMap.HandleAttribute(new TestAttribute("Test1", AttributeType.Temporary));
            changed = attributeMap.Finish();
            Assert(changed, "Map should report changes after adding an attribute");
            
            Console.WriteLine("Prepare and Finish methods test passed!");
        }

        private static void TestHandleAttribute()
        {
            Console.WriteLine("Testing HandleAttribute method...");
            
            var attributeMap = new AttributeMap();
            attributeMap.Prepare();
            
            // Add attributes
            var attr1 = new TestAttribute("Test1", AttributeType.Temporary);
            var attr2 = new TestAttribute("Test2", AttributeType.StructuredBuffer);
            var attr3 = new TestAttribute("Test1", AttributeType.Temporary); // Same name as attr1
            
            attributeMap.HandleAttribute(attr1);
            attributeMap.HandleAttribute(attr2);
            attributeMap.HandleAttribute(attr3);
            
            // Check attribute names
            var names = attributeMap.GetAttributeNames();
            Assert(names.Count == 2, "Should have 2 unique attribute names");
            Assert(names.Any(n => n == "Test1"), "Should contain Test1");
            Assert(names.Any(n => n == "Test2"), "Should contain Test2");
            
            // Check instances
            var instances1 = attributeMap.GetAttributeInstances("Test1");
            Assert(instances1.Count == 2, "Should have 2 instances of Test1");
            
            var instances2 = attributeMap.GetAttributeInstances("Test2");
            Assert(instances2.Count == 1, "Should have 1 instance of Test2");
            
            Console.WriteLine("HandleAttribute method test passed!");
        }

        private static void TestAttributeChanges()
        {
            Console.WriteLine("Testing attribute changes detection...");
            
            var attributeMap = new AttributeMap();
            
            // First cycle - add attributes
            attributeMap.Prepare();
            attributeMap.HandleAttribute(new TestAttribute("Attr1", AttributeType.Temporary));
            attributeMap.HandleAttribute(new TestAttribute("Attr2", AttributeType.StructuredBuffer));
            bool changed = attributeMap.Finish();
            Assert(changed, "Should detect changes when adding initial attributes");
            
            // Second cycle - same attributes
            attributeMap.Prepare();
            attributeMap.HandleAttribute(new TestAttribute("Attr1", AttributeType.Temporary));
            attributeMap.HandleAttribute(new TestAttribute("Attr2", AttributeType.StructuredBuffer));
            changed = attributeMap.Finish();
            Assert(!changed, "Should not detect changes when attributes remain the same");
            
            // Third cycle - remove one attribute
            attributeMap.Prepare();
            attributeMap.HandleAttribute(new TestAttribute("Attr1", AttributeType.Temporary));
            changed = attributeMap.Finish();
            Assert(changed, "Should detect changes when an attribute is removed");
            
            // Fourth cycle - add a new attribute
            attributeMap.Prepare();
            attributeMap.HandleAttribute(new TestAttribute("Attr1", AttributeType.Temporary));
            attributeMap.HandleAttribute(new TestAttribute("Attr3", AttributeType.Texture));
            changed = attributeMap.Finish();
            Assert(changed, "Should detect changes when a new attribute is added");
            
            Console.WriteLine("Attribute changes detection test passed!");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
