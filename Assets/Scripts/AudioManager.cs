// AudioManager
// Persists across scenes (DontDestroyOnLoad); a copy in a newly loaded scene removes itself.
// Music picks a random track from a list, based on where you are:
//   Menu:    the main menu's tracks
//   Ambient: the normal level tracks
//   Safe:    the tracks used while inside a safe room zone
// Every button and toggle in each scene plays a random click when pressed.
// Sections: Monster (growls), Music, UI (button clicks), Player, Flashlight, Pickup, Glow Stick.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private enum MusicZone { None, Menu, Ambient, Safe }

    [Header("Monster")]
    public AudioClip[] monsterGrowls;
    [Range(0f, 1f)] public float growlVolume = 1f;
    public float growlRadius = 15f;   // The growl is only heard within this distance of the monster

    [Header("Music")]
    public string menuSceneName = "MainMenu";
    public AudioClip[] menuLoops;         // Played on the main menu
    public AudioClip[] ambientLoops;      // Picked at random while exploring
    public AudioClip[] safeRoomLoops;     // Picked at random while inside a safe room
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public float crossfadeSeconds = 2f;   // Fade-out and fade-in length when switching tracks

    [Header("UI")]
    public AudioClip[] buttonClicks;      // Played when any button or toggle is pressed
    [Range(0f, 1f)] public float buttonClickVolume = 0.8f;

    [Header("Player")]
    public AudioClip[] footsteps;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;
    [Range(0f, 0.2f)] public float footstepPitchVariance = 0.1f;
    public AudioClip breathingLoop;
    [Range(0f, 1f)] public float breathingVolume = 0.7f;
    public float breathingFadeSpeed = 2f;
    [Tooltip("Shortest silence between growls, in seconds")]
    public float growlGapMin = 8f;
    [Tooltip("Longest silence between growls, in seconds")]
    public float growlGapMax = 20f;

    private float growlCooldown;
    private int lastGrowlIndex = -1;


    [Header("Flashlight")]
    public AudioClip modeSwitchClick;
    public AudioClip flashUse;

    [Header("Pickup")]
    public AudioClip pickupSound;

    [Header("Glow Stick")]
    public AudioClip glowStickCrack;

    private AudioSource musicSource;
    private AudioSource footstepSource;
    private AudioSource breathingSource;
    private AudioSource effectSource;
    private AudioSource uiSource;
    private AudioSource growlSource;

    private bool outOfBreath;
    private Transform player;
    private MusicZone currentZone = MusicZone.None;
    private bool switchingMusic;
    private float musicLevel = 1f;   // Fade multiplier, 0 to 1
    private AudioClip lastMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = CreateSource();
        musicSource.loop = false;

        footstepSource = CreateSource();

        breathingSource = CreateSource();
        breathingSource.loop = true;
        breathingSource.volume = 0f;

        effectSource = CreateSource();

        // Button clicks keep playing while the game is paused
        uiSource = CreateSource();
        uiSource.ignoreListenerPause = true;

        // The growl is a 3D source on its own child object, so it can move to the monster
        GameObject growlObject = new GameObject("Monster Growl");
        growlObject.transform.SetParent(transform);
        growlSource = growlObject.AddComponent<AudioSource>();
        growlSource.playOnAwake = false;
        growlSource.loop = false;
        growlSource.spatialBlend = 1f;   // Fully 3D: pans and fades with distance
        growlSource.rolloffMode = AudioRolloffMode.Linear;
        growlSource.minDistance = 1f;
        growlSource.maxDistance = growlRadius;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        WireButtons();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WireButtons();
    }

    private void Update()
    {
        // The player is in a new scene after each load, so look it up again if it's gone
        if (player == null)
        {
            FirstPersonController fpc = FindObjectOfType<FirstPersonController>();
            player = fpc != null ? fpc.transform : null;
        }

        // Music: switch when the zone changes (menu, ambient, or safe room), or when a track ends
        MusicZone desired = DesiredZone();

        if (!switchingMusic)
        {
            if (desired != currentZone)
            {
                currentZone = desired;
                StartCoroutine(SwitchMusic(PickTrack(CurrentPool())));
            }
            else if (!musicSource.isPlaying)
            {
                AudioClip[] pool = CurrentPool();
                if (pool != null && pool.Length > 0)
                    StartCoroutine(SwitchMusic(PickTrack(pool)));
            }
        }

        musicSource.volume = musicVolume * GameSettings.MusicVolume * musicLevel;

        // Breathing fades in while out of stamina and fades out once it recovers
        float target = outOfBreath ? breathingVolume * GameSettings.SfxVolume : 0f;
        breathingSource.volume = Mathf.MoveTowards(breathingSource.volume, target, breathingFadeSpeed * Time.deltaTime);

        if (breathingSource.volume > 0.001f && !breathingSource.isPlaying && breathingSource.clip != null)
            breathingSource.Play();
        else if (breathingSource.volume <= 0.001f && breathingSource.isPlaying)
            breathingSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- UI ----

    // Adds the click sound to every button and toggle in the scene
    private void WireButtons()
    {
        foreach (Button b in FindObjectsOfType<Button>(true))
            b.onClick.AddListener(PlayButtonClick);

        foreach (Toggle t in FindObjectsOfType<Toggle>(true))
            t.onValueChanged.AddListener(v => PlayButtonClick());
    }

    public void PlayButtonClick()
    {
        if (buttonClicks == null || buttonClicks.Length == 0) return;

        AudioClip clip = buttonClicks[Random.Range(0, buttonClicks.Length)];
        uiSource.PlayOneShot(clip, buttonClickVolume * GameSettings.SfxVolume);
    }

    // ---- Music ----

    // Where the music should be right now
    private MusicZone DesiredZone()
    {
        if (SceneManager.GetActiveScene().name == menuSceneName)
            return MusicZone.Menu;

        if (player != null && SafeRoom.Containing(player.position) != null)
            return MusicZone.Safe;

        return MusicZone.Ambient;
    }

    private AudioClip[] CurrentPool()
    {
        switch (currentZone)
        {
            case MusicZone.Menu: return menuLoops;
            case MusicZone.Safe: return safeRoomLoops;
            case MusicZone.Ambient: return ambientLoops;
            default: return null;
        }
    }

    // Picks a random track from the pool, avoiding the one that just played
    private AudioClip PickTrack(AudioClip[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) return pool[0];

        AudioClip pick = pool[Random.Range(0, pool.Length)];
        for (int i = 0; i < 10 && pick == lastMusic; i++)
            pick = pool[Random.Range(0, pool.Length)];

        return pick;
    }

    // Fades the current track out, swaps to the next one, and fades it back in
    private IEnumerator SwitchMusic(AudioClip next)
    {
        switchingMusic = true;

        if (musicSource.isPlaying)
        {
            for (float t = 0f; t < crossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                musicLevel = 1f - t / crossfadeSeconds;
                yield return null;
            }
        }

        musicSource.Stop();
        musicLevel = 0f;

        if (next == null)
        {
            switchingMusic = false;
            yield break;
        }

        musicSource.clip = next;
        musicSource.Play();
        lastMusic = next;

        for (float t = 0f; t < crossfadeSeconds; t += Time.unscaledDeltaTime)
        {
            musicLevel = t / crossfadeSeconds;
            yield return null;
        }

        musicLevel = 1f;
        switchingMusic = false;
    }

    // ---- Monster ----

    // Called by a monster every frame. Starts the growl when the listener is inside the
    // radius, keeps the sound at the monster's position, and stops it when they leave.
// Called by a monster every frame. Plays one random growl while the listener is in range,
// then waits a random gap before the next one. Stops immediately if the listener leaves.
    public void UpdateMonsterGrowl(Vector3 monsterPosition, Vector3 listenerPosition)
    {
        growlSource.transform.position = monsterPosition;
        growlSource.volume = growlVolume * GameSettings.SfxVolume;
        growlSource.maxDistance = growlRadius;

        bool inRange = Vector3.Distance(monsterPosition, listenerPosition) <= growlRadius;

        if (!inRange)
        {
            if (growlSource.isPlaying) growlSource.Stop();
            return;
        }

        // Still playing the last growl: let it finish
        if (growlSource.isPlaying) return;

        // Silent: count down the gap before the next growl
        growlCooldown -= Time.deltaTime;
        if (growlCooldown > 0f) return;

        if (monsterGrowls == null || monsterGrowls.Length == 0) return;

        growlSource.clip = PickGrowl();
        growlSource.Play();
        growlCooldown = Random.Range(growlGapMin, growlGapMax);
    }

    // A random growl, never the same one twice in a row
    private AudioClip PickGrowl()
    {
        if (monsterGrowls.Length == 1)
        {
            lastGrowlIndex = 0;
            return monsterGrowls[0];
        }

        int index = Random.Range(0, monsterGrowls.Length);
        if (index == lastGrowlIndex)
            index = (index + 1 + Random.Range(0, monsterGrowls.Length - 1)) % monsterGrowls.Length;

        lastGrowlIndex = index;
        return monsterGrowls[index];
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

    private void PlayEffect(AudioClip clip)
    {
        if (clip == null) return;
        effectSource.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D: heard the same from anywhere
        return source;
    }
}
