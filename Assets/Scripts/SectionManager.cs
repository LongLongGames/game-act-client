using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Minimal Section (Zone) manager.
    /// 
    /// Current stage: single active section is enough.
    /// Later can expand to multiple 200x200 sections connected by bridges.
    /// 
    /// Each Section owns its own ISpatialIndex instance.
    /// </summary>
    public class SectionManager
    {
        public class Section
        {
            public int Id;
            public string Name;
            public Bounds WorldBounds;          // approximate world bounds of this section
            public ISpatialIndex Spatial;       // the spatial index for this section
            public bool IsActive = true;

            // Future: list of connected section ids (bridges / portals)
            public List<int> ConnectedSectionIds = new List<int>();
        }

        private readonly Dictionary<int, Section> _sections = new Dictionary<int, Section>();
        private int _nextSectionId = 1;
        private Section _activeSection;

        /// <summary>Currently focused section (usually the one player is in).</summary>
        public Section ActiveSection => _activeSection;

        /// <summary>All sections.</summary>
        public IReadOnlyDictionary<int, Section> Sections => _sections;

        /// <summary>
        /// Create a new section with its own SpatialHash.
        /// </summary>
        public Section CreateSection(string name, Bounds worldBounds, float cellSize = 10f)
        {
            var section = new Section
            {
                Id = _nextSectionId++,
                Name = name,
                WorldBounds = worldBounds,
                Spatial = new SpatialHash(cellSize),
                IsActive = true
            };

            _sections[section.Id] = section;

            // First section becomes active by default
            if (_activeSection == null)
                _activeSection = section;

            return section;
        }

        /// <summary>Set the active section (player entered this zone).</summary>
        public void SetActiveSection(int sectionId)
        {
            if (_sections.TryGetValue(sectionId, out var section))
            {
                _activeSection = section;
            }
        }

        /// <summary>
        /// Convenience: get the spatial index of the active section.
        /// Most gameplay code can just use this in early stages.
        /// </summary>
        public ISpatialIndex ActiveSpatial => _activeSection?.Spatial;

        /// <summary>
        /// Find which section contains a world position (simple bounds test).
        /// Returns null if none.
        /// </summary>
        public Section FindSectionAt(Vector3 worldPos)
        {
            foreach (var kv in _sections)
            {
                if (kv.Value.WorldBounds.Contains(worldPos))
                    return kv.Value;
            }
            return null;
        }

        /// <summary>
        /// Register connection between two sections (bridge / portal).
        /// </summary>
        public void ConnectSections(int sectionA, int sectionB)
        {
            if (_sections.TryGetValue(sectionA, out var a) &&
                _sections.TryGetValue(sectionB, out var b))
            {
                if (!a.ConnectedSectionIds.Contains(sectionB))
                    a.ConnectedSectionIds.Add(sectionB);
                if (!b.ConnectedSectionIds.Contains(sectionA))
                    b.ConnectedSectionIds.Add(sectionA);
            }
        }

        /// <summary>
        /// Get active section + all directly connected sections.
        /// Useful for AOI that needs to look into neighboring zones.
        /// </summary>
        public void GetRelevantSections(List<Section> results)
        {
            results.Clear();
            if (_activeSection == null) return;

            results.Add(_activeSection);
            foreach (int id in _activeSection.ConnectedSectionIds)
            {
                if (_sections.TryGetValue(id, out var s) && s.IsActive)
                    results.Add(s);
            }
        }

        public void ClearAll()
        {
            foreach (var kv in _sections)
            {
                kv.Value.Spatial?.Clear();
            }
            _sections.Clear();
            _activeSection = null;
            _nextSectionId = 1;
        }
    }
}