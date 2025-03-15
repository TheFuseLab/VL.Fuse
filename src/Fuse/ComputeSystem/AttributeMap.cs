using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using Stride.Core.Mathematics;

namespace Fuse.ComputeSystem
{
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
}
