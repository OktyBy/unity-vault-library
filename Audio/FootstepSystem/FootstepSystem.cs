using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Audio
{
    /// <summary>
    /// Surface-based footstep audio system.
    /// </summary>
    public class FootstepSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private Transform footPosition;
        [SerializeField] private float raycastDistance = 0.5f;
        [SerializeField] private LayerMask groundLayers;

        [Header("Footstep Settings")]
        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float runStepInterval = 0.3f;
        [SerializeField] private float sneakStepInterval = 0.8f;
        [SerializeField] private float minimumVelocity = 0.1f;

        [Header("Volume")]
        [SerializeField] private float walkVolume = 0.5f;
        [SerializeField] private float runVolume = 0.8f;
        [SerializeField] private float sneakVolume = 0.2f;
        [SerializeField] private float landingVolume = 1f;

        [Header("Surfaces")]
        [SerializeField] private List<SurfaceDefinition> surfaces = new List<SurfaceDefinition>();
        [SerializeField] private SurfaceDefinition defaultSurface;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool use3DAudio = true;
        [SerializeField] private float pitchVariation = 0.1f;

        // State
        private float stepTimer;
        private float currentStepInterval;
        private float currentVolume;
        private bool isGrounded;
        private bool wasGrounded;
        private SurfaceType currentSurface = SurfaceType.Default;
        private MovementType currentMovement = MovementType.Walk;

        // Components
        private CharacterController characterController;
        private Rigidbody rb;

        // Events
        public event Action<SurfaceType> Footstep;
        public event Action<SurfaceType, float> Landed;

        public enum MovementType
        {
            Walk,
            Run,
            Sneak
        }

        public enum SurfaceType
        {
            Default,
            Grass,
            Dirt,
            Stone,
            Wood,
            Metal,
            Water,
            Snow,
            Sand,
            Carpet,
            Tile
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = use3DAudio ? 1f : 0f;

            if (footPosition == null)
            {
                footPosition = transform;
            }

            SetupDefaultSurfaces();
            UpdateMovementSettings();
        }

        private void SetupDefaultSurfaces()
        {
            if (surfaces.Count == 0 && defaultSurface == null)
            {
                // Create minimal default
                defaultSurface = new SurfaceDefinition
                {
                    surfaceType = SurfaceType.Default,
                    footstepClips = new AudioClip[0]
                };
            }
        }

        private void Update()
        {
            DetectSurface();
            DetectGrounded();
            UpdateFootsteps();
        }

        private void DetectSurface()
        {
            Vector3 origin = footPosition.position + Vector3.up * 0.1f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayers))
            {
                // Check by tag
                SurfaceDefinition surface = GetSurfaceByTag(hit.collider.tag);

                // Check by texture (terrain)
                if (surface == null && hit.collider is TerrainCollider)
                {
                    surface = GetSurfaceFromTerrain(hit.point);
                }

                // Check by material
                if (surface == null)
                {
                    surface = GetSurfaceByMaterial(hit.collider);
                }

                // Default
                if (surface == null)
                {
                    surface = defaultSurface;
                }

                if (surface != null)
                {
                    currentSurface = surface.surfaceType;
                }
            }
        }

        private SurfaceDefinition GetSurfaceByTag(string tag)
        {
            foreach (var surface in surfaces)
            {
                if (surface.tags != null)
                {
                    foreach (var surfaceTag in surface.tags)
                    {
                        if (tag == surfaceTag)
                        {
                            return surface;
                        }
                    }
                }
            }
            return null;
        }

        private SurfaceDefinition GetSurfaceFromTerrain(Vector3 worldPos)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return null;

            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;

            // Get terrain coordinates
            int mapX = (int)(((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth);
            int mapZ = (int)(((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight);

            mapX = Mathf.Clamp(mapX, 0, terrainData.alphamapWidth - 1);
            mapZ = Mathf.Clamp(mapZ, 0, terrainData.alphamapHeight - 1);

            float[,,] alphaMap = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

            // Find dominant texture
            int dominantIndex = 0;
            float dominantWeight = 0;

            for (int i = 0; i < alphaMap.GetLength(2); i++)
            {
                if (alphaMap[0, 0, i] > dominantWeight)
                {
                    dominantWeight = alphaMap[0, 0, i];
                    dominantIndex = i;
                }
            }

            // Map terrain layer to surface
            foreach (var surface in surfaces)
            {
                if (surface.terrainLayerIndices != null)
                {
                    foreach (int index in surface.terrainLayerIndices)
                    {
                        if (index == dominantIndex)
                        {
                            return surface;
                        }
                    }
                }
            }

            return null;
        }

        private SurfaceDefinition GetSurfaceByMaterial(Collider collider)
        {
            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null) return null;

            string materialName = renderer.sharedMaterial?.name?.ToLower() ?? "";

            foreach (var surface in surfaces)
            {
                if (surface.materialKeywords != null)
                {
                    foreach (var keyword in surface.materialKeywords)
                    {
                        if (materialName.Contains(keyword.ToLower()))
                        {
                            return surface;
                        }
                    }
                }
            }

            return null;
        }

        private void DetectGrounded()
        {
            wasGrounded = isGrounded;

            if (characterController != null)
            {
                isGrounded = characterController.isGrounded;
            }
            else
            {
                isGrounded = Physics.Raycast(footPosition.position + Vector3.up * 0.1f, Vector3.down, raycastDistance, groundLayers);
            }

            // Landing detection
            if (isGrounded && !wasGrounded)
            {
                float fallVelocity = 0;
                if (characterController != null)
                {
                    fallVelocity = Mathf.Abs(characterController.velocity.y);
                }
                else if (rb != null)
                {
                    fallVelocity = Mathf.Abs(rb.velocity.y);
                }

                if (fallVelocity > 2f)
                {
                    PlayLandingSound(fallVelocity);
                }
            }
        }

        private void UpdateFootsteps()
        {
            if (!isGrounded) return;

            float velocity = GetHorizontalVelocity();
            if (velocity < minimumVelocity) return;

            stepTimer += Time.deltaTime;

            if (stepTimer >= currentStepInterval)
            {
                stepTimer = 0;
                PlayFootstep();
            }
        }

        private float GetHorizontalVelocity()
        {
            Vector3 velocity = Vector3.zero;

            if (characterController != null)
            {
                velocity = characterController.velocity;
            }
            else if (rb != null)
            {
                velocity = rb.velocity;
            }

            velocity.y = 0;
            return velocity.magnitude;
        }

        private void PlayFootstep()
        {
            SurfaceDefinition surface = GetCurrentSurface();
            if (surface == null || surface.footstepClips == null || surface.footstepClips.Length == 0)
            {
                return;
            }

            AudioClip clip = surface.footstepClips[UnityEngine.Random.Range(0, surface.footstepClips.Length)];

            float pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, currentVolume * surface.volumeMultiplier);

            Footstep?.Invoke(currentSurface);
        }

        private void PlayLandingSound(float fallVelocity)
        {
            SurfaceDefinition surface = GetCurrentSurface();
            if (surface == null) return;

            AudioClip clip = null;

            if (surface.landingClips != null && surface.landingClips.Length > 0)
            {
                clip = surface.landingClips[UnityEngine.Random.Range(0, surface.landingClips.Length)];
            }
            else if (surface.footstepClips != null && surface.footstepClips.Length > 0)
            {
                clip = surface.footstepClips[UnityEngine.Random.Range(0, surface.footstepClips.Length)];
            }

            if (clip != null)
            {
                float volume = Mathf.Lerp(landingVolume * 0.5f, landingVolume, fallVelocity / 10f);
                audioSource.PlayOneShot(clip, volume * surface.volumeMultiplier);
            }

            Landed?.Invoke(currentSurface, fallVelocity);
        }

        private SurfaceDefinition GetCurrentSurface()
        {
            foreach (var surface in surfaces)
            {
                if (surface.surfaceType == currentSurface)
                {
                    return surface;
                }
            }
            return defaultSurface;
        }

        /// <summary>
        /// Set movement type (walk, run, sneak).
        /// </summary>
        public void SetMovementType(MovementType type)
        {
            currentMovement = type;
            UpdateMovementSettings();
        }

        private void UpdateMovementSettings()
        {
            switch (currentMovement)
            {
                case MovementType.Walk:
                    currentStepInterval = walkStepInterval;
                    currentVolume = walkVolume;
                    break;
                case MovementType.Run:
                    currentStepInterval = runStepInterval;
                    currentVolume = runVolume;
                    break;
                case MovementType.Sneak:
                    currentStepInterval = sneakStepInterval;
                    currentVolume = sneakVolume;
                    break;
            }
        }

        /// <summary>
        /// Manually trigger a footstep.
        /// </summary>
        public void TriggerFootstep()
        {
            PlayFootstep();
        }

        /// <summary>
        /// Set step interval directly.
        /// </summary>
        public void SetStepInterval(float interval)
        {
            currentStepInterval = interval;
        }

        /// <summary>
        /// Force a specific surface type.
        /// </summary>
        public void ForceSurface(SurfaceType type)
        {
            currentSurface = type;
        }
    }

    [Serializable]
    public class SurfaceDefinition
    {
        public SurfaceType surfaceType;
        public AudioClip[] footstepClips;
        public AudioClip[] landingClips;
        public float volumeMultiplier = 1f;
        public string[] tags;
        public string[] materialKeywords;
        public int[] terrainLayerIndices;
    }

    public enum SurfaceType
    {
        Default,
        Grass,
        Dirt,
        Stone,
        Wood,
        Metal,
        Water,
        Snow,
        Sand,
        Carpet,
        Tile
    }
}
