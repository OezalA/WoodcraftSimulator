using UnityEngine;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Inventory;

namespace WoodcraftSimulator.Player
{
    /// <summary>
    /// Placeholder feedback sounds around the player (Phase 8d, D-044): a knock when picking up or putting down
    /// (boards and tools), tones when money comes in or goes out, and a quiet room tone. Tool sounds live on the tools.
    /// </summary>
    public sealed class PlayerSounds : MonoBehaviour
    {
        [SerializeField] private PlayerCarrier carrier;
        [SerializeField] private PlayerToolBelt toolBelt;

        [Tooltip("Optional: money tones on purchases and pay.")]
        [SerializeField] private WorkshopEconomy economy;

        [Tooltip("Optional: recorded CC0 sounds (Phase 13d, D-061); without them the generated placeholders play.")]
        [SerializeField] private Audio.SoundLibrary library;

        [Tooltip("Optional: a confirmation or error sound when the job board answers (Phase 13e, D-062).")]
        [SerializeField] private Jobs.JobBoard jobBoard;

        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float interfaceVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.12f;

        private AudioSource effects;
        private Money lastBalance;
        private int lastBeltCount = -1;
        private Carryable lastCarried;

        private void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;

            var ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = ProceduralSounds.Ambience;
            ambience.loop = true;
            ambience.volume = ambienceVolume;
            ambience.Play();
        }

        private void OnEnable()
        {
            if (carrier != null)
            {
                carrier.CarryingChanged += OnCarryingChanged;
            }

            if (toolBelt != null)
            {
                toolBelt.Changed += OnBeltChanged;
            }

            if (economy != null)
            {
                lastBalance = economy.Ledger.Balance;
                economy.Ledger.Changed += OnLedgerChanged;
            }

            if (jobBoard != null)
            {
                jobBoard.Answered += OnJobBoardAnswered;
            }
        }

        private void OnDisable()
        {
            if (carrier != null)
            {
                carrier.CarryingChanged -= OnCarryingChanged;
            }

            if (toolBelt != null)
            {
                toolBelt.Changed -= OnBeltChanged;
            }

            if (economy != null)
            {
                economy.Ledger.Changed -= OnLedgerChanged;
            }

            if (jobBoard != null)
            {
                jobBoard.Answered -= OnJobBoardAnswered;
            }
        }

        // Accepted, delivered, bought: a confirmation; refused: an error tone (recorded sounds only).
        private void OnJobBoardAnswered(bool accepted)
        {
            if (library != null && effects != null)
            {
                AudioClip clip = Audio.SoundLibrary.Any(accepted ? library.uiConfirm : library.uiError);
                if (clip != null)
                {
                    effects.PlayOneShot(clip, interfaceVolume);
                }
            }
        }

        // Picking up and putting down knock with what it is: wood, glass, metal (the stepladder) (D-061).
        private void OnCarryingChanged(bool isCarrying)
        {
            if (isCarrying)
            {
                lastCarried = carrier.Carried;
            }

            Play(ImpactFor(lastCarried) ?? ProceduralSounds.Thunk);
        }

        private AudioClip ImpactFor(Carryable thing)
        {
            if (library == null || thing == null)
            {
                return null;
            }

            if (thing.TryGetComponent(out Materials.TimberBoard board) && board.Piece != null)
            {
                if (board.Piece.Species.Id == "glass")
                {
                    return Audio.SoundLibrary.Any(library.impactGlass);
                }

                return board.Piece.Species.IsTimber ? Audio.SoundLibrary.Any(library.impactWood) : null;
            }

            return Audio.SoundLibrary.Any(library.impactMetal);
        }

        private void OnBeltChanged()
        {
            // Only taking a tool or hanging it back makes a sound, not switching between tools.
            int count = 0;
            for (int i = 0; i < toolBelt.Belt.SlotCount; i++)
            {
                if (toolBelt.Belt.GetItem(i) != null)
                {
                    count++;
                }
            }

            if (lastBeltCount >= 0 && count != lastBeltCount)
            {
                Play(ProceduralSounds.Thunk);
            }

            lastBeltCount = count;
        }

        private void OnLedgerChanged()
        {
            Money balance = economy.Ledger.Balance;
            // The first booking of a new workshop (starting funds) is silent.
            if (economy.Ledger.Entries.Count > 1 && balance != lastBalance)
            {
                Play(balance > lastBalance ? ProceduralSounds.CoinIn : ProceduralSounds.CoinOut);
            }

            lastBalance = balance;
        }

        private void Play(AudioClip clip)
        {
            if (effects != null && clip != null && Time.timeSinceLevelLoad > 1f)
            {
                effects.PlayOneShot(clip, effectsVolume);
            }
        }
    }
}
