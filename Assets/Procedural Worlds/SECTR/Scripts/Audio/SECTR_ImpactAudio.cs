// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

/// \ingroup Audio
/// Playes a SECTR_AudioCue when a physics impact is detected.
/// 
/// ImpactSource supports any collider that Unity allows, 
/// provided it's setup to create and recieve collision.
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Impact Audio")]
public class SECTR_ImpactAudio : MonoBehaviour 
{
	#region Private Details
	private float nextImpactTime = 0;
#if UNITY_6000_0_OR_NEWER
    private Dictionary<PhysicsMaterial, ImpactSound> surfaceTable = null;
#else
	private Dictionary<PhysicMaterial, ImpactSound> surfaceTable = null;
#endif
    #endregion

    #region Public Interface
    [System.Serializable]
	public class ImpactSound
	{
		
#if UNITY_6000_0_OR_NEWER
        public PhysicsMaterial SurfaceMaterial = null;
#else
	public PhysicMaterial SurfaceMaterial = null;
#endif
        public SECTR_AudioCue ImpactCue = null;
	}

	[SECTR_ToolTip("Default sound to play on impact.")]
	public ImpactSound DefaultSound = null;
	[SECTR_ToolTip("Surface specific impact sounds.")]
	public List<ImpactSound> SurfaceImpacts = new List<ImpactSound>();
	[SECTR_ToolTip("The minimum relative speed at the time of impact required to trigger this cue.")]
	public float MinImpactSpeed = .01f;
	[SECTR_ToolTip("The minimum amount of time between playback of this sound.")]
	public float MinImpactInterval = 0.5f;
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		int numSurfaces = SurfaceImpacts.Count;
		for(int surfaceIndex = 0; surfaceIndex < numSurfaces; ++surfaceIndex)
		{
			ImpactSound impactSound = SurfaceImpacts[surfaceIndex];
			if(impactSound.SurfaceMaterial != null)
			{
				if(surfaceTable == null)
				{
					#if UNITY_6000_0_OR_NEWER
						surfaceTable = new Dictionary<PhysicsMaterial, ImpactSound>();
					#else
						surfaceTable = new Dictionary<PhysicMaterial, ImpactSound>();
					#endif
                }
				surfaceTable[impactSound.SurfaceMaterial] = impactSound;
			}
		}
	}

	void OnDisable()
	{
		surfaceTable = null;
	}

	void OnCollisionStay(Collision collision)
	{
		if(Time.time >= nextImpactTime && collision != null && collision.contacts.Length > 0 && collision.relativeVelocity.sqrMagnitude >= MinImpactSpeed * MinImpactSpeed)
		{
			ImpactSound impactSound;
			if(collision.collider.sharedMaterial == null || surfaceTable == null || !surfaceTable.TryGetValue(collision.collider.sharedMaterial, out impactSound))
			{
				impactSound = DefaultSound;
			}
			//Debug.Log(collision.contacts[0].point);
			
			SECTR_AudioSystem.Play(impactSound.ImpactCue, collision.contacts[0].point, false);
			nextImpactTime = Time.time + MinImpactInterval;
		}
	}
	#endregion
}
