using UnityEngine;
using System.Collections.Generic;

namespace ChainPlacement
{
    [SelectionBase]
    [DisallowMultipleComponent]
    public class ChainSegment : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The segment library this segment belongs to")]
        public SegmentLibrary library;
        
        [Tooltip("Index of this segment's definition in the library")]
        public int segmentIndex = -1;
        
        [Header("Chain Hierarchy")]
        [Tooltip("Parent segment in the chain")]
        public ChainSegment parentSegment;
        
        [Tooltip("Spawn pivot index on the parent that this segment was spawned from")]
        public int parentSpawnPivotIndex = 0;
        
        [Tooltip("Child segments in the chain (one per spawn pivot)")]
        public List<ChainSegment> childSegments = new List<ChainSegment>();
        
        [Header("Spawn Pivots (Set by Definition)")]
        [Tooltip("Spawn pivot data copied from definition")]
        public List<SpawnPivot> spawnPivots = new List<SpawnPivot>();
        
        [Header("Legacy (Auto-Migrated)")]
        public Vector3 spawnPivotLocalPosition;
        public Vector3 spawnPivotLocalRotation;
        
        public SegmentDefinition Definition
        {
            get
            {
                if (library == null || segmentIndex < 0)
                    return null;
                
                return library.GetSegment(segmentIndex);
            }
        }
        
        public Vector3 GetSpawnPivotWorldPosition(int pivotIndex)
        {
            var pivot = GetSpawnPivot(pivotIndex);
            if (pivot == null)
                return transform.position;
            
            return transform.TransformPoint(pivot.position);
        }
        
        public Quaternion GetSpawnPivotWorldRotation(int pivotIndex)
        {
            var pivot = GetSpawnPivot(pivotIndex);
            if (pivot == null)
                return transform.rotation;
            
            Quaternion localRot = Quaternion.Euler(pivot.rotation);
            return transform.rotation * localRot;
        }
        
        public SpawnPivot GetSpawnPivot(int index)
        {
            if (spawnPivots == null || spawnPivots.Count == 0)
            {
                MigrateLegacyPivot();
            }
            
            if (index < 0 || index >= spawnPivots.Count)
                return null;
            
            return spawnPivots[index];
        }
        
        public bool CanSpawnChildAt(int pivotIndex)
        {
            var def = Definition;
            if (def == null || !def.canHaveChildren)
                return false;
            
            if (pivotIndex < 0 || pivotIndex >= spawnPivots.Count)
                return false;
            
            return GetChildAt(pivotIndex) == null;
        }
        
        public ChainSegment GetChildAt(int pivotIndex)
        {
            if (childSegments == null)
                childSegments = new List<ChainSegment>();
            
            foreach (var child in childSegments)
            {
                if (child != null && child.parentSpawnPivotIndex == pivotIndex)
                    return child;
            }
            
            return null;
        }
        
        public void InitializeFromDefinition(SegmentLibrary lib, int index)
        {
            library = lib;
            segmentIndex = index;
            
            var def = Definition;
            if (def != null)
            {
                def.MigrateLegacyPivot();
                
                spawnPivots = new List<SpawnPivot>();
                
                foreach (var pivot in def.spawnPivots)
                {
                    spawnPivots.Add(new SpawnPivot(pivot.name, pivot.position, pivot.rotation));
                }
            }
        }
        
        public void SetChildSegment(ChainSegment child, int pivotIndex)
        {
            if (child == null)
                return;
            
            if (childSegments == null)
                childSegments = new List<ChainSegment>();
            
            var existingChild = GetChildAt(pivotIndex);
            if (existingChild != null)
            {
                Debug.LogWarning($"Spawn pivot {pivotIndex} already has a child!");
                return;
            }
            
            childSegments.Add(child);
            child.parentSegment = this;
            child.parentSpawnPivotIndex = pivotIndex;
        }
        
        public void RemoveChildSegment(ChainSegment child)
        {
            if (child != null && childSegments != null)
            {
                childSegments.Remove(child);
                child.parentSegment = null;
            }
        }
        
        private void MigrateLegacyPivot()
        {
            if (spawnPivots == null || spawnPivots.Count == 0)
            {
                spawnPivots = new List<SpawnPivot>
                {
                    new SpawnPivot("Forward", spawnPivotLocalPosition, spawnPivotLocalRotation)
                };
            }
        }
        
        private void OnDrawGizmos()
        {
            if (library == null)
                return;
            
            if (spawnPivots == null || spawnPivots.Count == 0)
            {
                MigrateLegacyPivot();
            }
            
            for (int i = 0; i < spawnPivots.Count; i++)
            {
                DrawPivotGizmo(i, false);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (library == null)
                return;
            
            if (spawnPivots == null || spawnPivots.Count == 0)
            {
                MigrateLegacyPivot();
            }
            
            for (int i = 0; i < spawnPivots.Count; i++)
            {
                DrawPivotGizmo(i, true);
            }
        }
        
        private void DrawPivotGizmo(int pivotIndex, bool selected)
        {
            var pivot = GetSpawnPivot(pivotIndex);
            if (pivot == null)
                return;
            
            Vector3 pivotPos = GetSpawnPivotWorldPosition(pivotIndex);
            Quaternion pivotRot = GetSpawnPivotWorldRotation(pivotIndex);
            
            bool hasChild = GetChildAt(pivotIndex) != null;
            
            if (hasChild)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            else
            {
                Gizmos.color = library.pivotGizmoColor;
            }
            
            float size = selected ? 0.15f : 0.1f;
            Gizmos.DrawWireSphere(pivotPos, size);
            
            if (!hasChild)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(pivotPos, pivotRot * Vector3.right * 0.3f);
                
                Gizmos.color = Color.green;
                Gizmos.DrawRay(pivotPos, pivotRot * Vector3.up * 0.3f);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(pivotPos, pivotRot * Vector3.forward * 0.3f);
            }
        }
        
        [System.Obsolete("Use GetSpawnPivotWorldPosition(int) instead")]
        public Vector3 SpawnPivotWorldPosition => GetSpawnPivotWorldPosition(0);
        
        [System.Obsolete("Use GetSpawnPivotWorldRotation(int) instead")]
        public Quaternion SpawnPivotWorldRotation => GetSpawnPivotWorldRotation(0);
        
        [System.Obsolete("Use CanSpawnChildAt(int) instead")]
        public bool CanSpawnChild => CanSpawnChildAt(0);
        
        [System.Obsolete("Use GetChildAt(int) instead")]
        public ChainSegment childSegment
        {
            get => GetChildAt(0);
            set
            {
                if (value != null)
                    SetChildSegment(value, 0);
            }
        }
    }
}
