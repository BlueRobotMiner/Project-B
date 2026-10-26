// AudioManager
// Persists across scenes (DontDestroyOnLoad); a copy in a newly loaded scene removes itself.
// Sections: Monster (3D growl that plays only within growlRadius of the monster),
// Ambient (loop), Player (footsteps, breathing), Flashlight (click, flash),
// Pickup (item pickups), Glow Stick (crack).
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Monster")]
    public AudioClip[] monsterGrowls;
    [Range(0f, 1f)] public float growlVolume = 1f;
    public float growlRadius = 15f;   // The growl is only heard within this distance of the monster

    [Header("Ambient")]
    public AudioClip ambientLoop;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;

    [Header("Player")]
    public AudioClip[] footsteps;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;
    [Range(0f, 0.2f)] public float footstepPitchVariance = 0.1f;
    public AudioClip breathingLoop;
    [Range(0f, 1f)] public float breathingVolume = 0.7f;
    public float breathingFadeSpeed = 2f;

    [Header("Flashlight")]
    public AudioClip modeSwitchClick;
    public AudioClip flashUse;

    [Header("Pickup")]
    public AudioClip pickupSound;

    [Header("Glow Stick")]
    public AudioClip glowStickCrack;

    private AudioSource ambientSource;
    private AudioSource footstepSource;
    private AudioSource breathingSource;
    private AudioSource effectSource;
    private AudioSource growlSource;
    private bool outOfBreath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ambientSource = CreateSource();
        ambientSource.clip = ambientLoop;
        ambientSource.loop = true;

        footstepSource = CreateSource();

        breathingSource = CreateSource();
        breathingSource.clip = breathingLoop;
        breathingSource.loop = true;
        breathingSource.volume = 0f;

        effectSource = CreateSource();

        // The growl is a 3D source on its own child object, so it can move to the monster
        GameObject growlObject = new GameObject("Monster Growl");
        growlObject.transform.SetParent(transform);
        growlSource = growlObject.AddComponent<AudioSource>();
        growlSource.playOnAwake = false;
        growlSource.loop = true;
        growlSource.spatialBlend = 1f;   // Fully 3D: pans and fades with distance
        growlSource.rolloffMode = AudioRolloffMode.Linear;
        growlSource.minDistance = 1f;
        growlSource.maxDistance = growlRadius;
    }

    void Start()
    {
        if (ambientSource.clip != null) ambientSource.Play();
    }

    void Update()
    {
        ambientSource.volume = ambientVolume * GameSettings.MusicVolume;

        float target = outOfBreath ? breathingVolume * GameSettings.SfxVolume : 0f;
        breathingSource.volume = Mathf.MoveTowards(breathingSource.volume, target, breathingFadeSpeed * Time.deltaTime);

        if (breathingSource.volume > 0.001f && !breathingSource.isPlaying && breathingSource.clip != null)
            breathingSource.Play();
        else if (breathingSource.volume <= 0.001f && breathingSource.isPlaying)
            breathingSource.Stop();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Monster ----

    // Called by a monster every frame. Starts the growl when the listener is inside the
    // radius, keeps the sound at the monster's position, and stops it when they leave.
    public void UpdateMonsterGrowl(Vector3 monsterPosition, Vector3 listenerPosition)
    {
        growlSource.transform.position = monsterPosition;
        growlSource.volume = growlVolume * GameSettings.SfxVolume;
        growlSource.maxDistance = growlRadius;

        bool inRange = Vector3.Distance(monsterPosition, listenerPosition) <= growlRadius;

        if (inRange && !growlSource.isPlaying)
        {
            if (monsterGrowls == null || monsterGrowls.Length == 0) return;
            growlSource.clip = monsterGrowls[Random.Range(0, monsterGrowls.Length)];
            growlSource.Play();
        }
        else if (!inRange && growlSource.isPlaying)
        {
            growlSource.Stop();
        }
    }

    // ---- Player ----

    // Called every frame by the player controller: true while stamina is empty.
    public void SetOutOfBreath(bool value)
    {
        outOfBreath = value;
    }

    public void PlayFootstep()
    {
        if (footsteps == null || footsteps.Length == 0) return;
        AudioClip clip = footsteps[Random.Range(0, footsteps.Length)];
        footstepSource.pitch = 1f + Random.Range(-footstepPitchVariance, footstepPitchVariance);
        footstepSource.PlayOneShot(clip, footstepVolume * GameSettings.SfxVolume);
    }

    // ---- Flashlight ----

    public void PlayFlashlightClick()
    {
        PlayEffect(modeSwitchClick);
    }

    public void PlayFlash()
    {
        PlayEffect(flashUse);
    }

    // ---- Pickup ----

    // Called when the player picks up an item (camera, parts, keys, flashlight)
    public void PlayPickup()
    {
        PlayEffect(pickupSound);
    }

    // ---- Glow Stick ----

    public void PlayGlowStickCrack(Vector3 position)
    {
        if (glowStickCrack == null) return;
        AudioSource.PlayClipAtPoint(glowStickCrack, position, GameSettings.SfxVolume);
    }

    // ---- Helpers ----

    void PlayEffect(AudioClip clip)
    {
        if (clip == null) return;
        effectSource.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D: heard the same from anywhere
        return source;
    }
}
