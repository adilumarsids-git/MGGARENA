using UnityEngine;
using TMPro;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    // For all details, How to use MOST_Gate and APIs
    // https://solo-player.gitbook.io/most-in-one/most-systems/most-gate

    [HideScriptField]
    public class MOST_Gate : MonoBehaviour
    {
        // You Can Add as many types as you can and switch between all types like this...
        // if (gate.Type == Most_Gate.GateType.FireRate)
        // {
        //    // Debug.Log("FireRate gate Triggered");
        // }
        // else if (gate.Type == Most_Gate.GateType.FireRange)
        // {
        //    // Debug.Log("FireRange gate Triggered");
        // }
        //
        // and so on...
        // see the link above for more details if you confused

        public enum GateType { Health, FireRate , FireRange , Upgrade , AddChilds ,Currency, Other}
        public enum GateSign { Plus, Minus, Subtract, Multiply, NoSign }
        [BigHeader("Main Settings")]
        [Tooltip("A read only check if this gate has triggered or not")]
        [ReadOnly] public bool IsCollected;
        [Tooltip("You can edit, add, remove these types to fit your game\nCheck GateType enum")] public GateType Type;
        
        [Tooltip("To Rescale the added value...")] // for example if the value is 1000 and the rescale is .01f the final result inside the code will be 10
        [Min(0)] public float AmountRescale = 1;

        [Tooltip("The Text Will be displayed and used in the gate output\n" +
            "Condition: to enable Math gates you have to add signs at the start of the numble")]
        public string GateText;

        [Tooltip("If enabled, positive sign will be hidden at Gate Text object")]
        public bool HidePositiveSign;

        [HideIfAll(nameof(Type), GateType.Currency,false, nameof(Type), GateType.Upgrade, false)]
        [BigHeader("Currency Set")]
        public MOST_Database CurrencyDataHolder;
        [HideIfAll(nameof(Type), GateType.Currency, false, nameof(Type), GateType.Upgrade, false)]
        public string CurrencyName;

        [BigHeader("Fixed Output")]
        [Tooltip("If Enabled, the gate will use OutputText as the gate value for calculations")]
        public bool EnableFixedOutput;
        [ReadOnlyIf(nameof(EnableFixedOutput),false), Tooltip("if true, the gate models will be controlled using OutputText value")]
        public bool FixedOutputControlGates;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("Fixed Gate Output")]
        public string OutputText;

        [BigHeader("Gate Models")]
        [InnerHint("(Optional)"), Tooltip("The displayed model if Gate Text output is a positive number")]
        public GameObject PositiveModel;
        [InnerHint("(Optional)"), Tooltip("The displayed model if Gate Text output is a negative number")]
        public GameObject NegativeModel;
        [InnerHint("(Optional)"), Tooltip("The displayed model if Gate Text output is a custom text (not a number)")]
        public GameObject CustomModel;

        [InnerHint("(Optional)"), Tooltip("The text mesh that will be used to display the main Gate Text (GateText)")]
        public TMP_Text GatePost;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), InnerHint("(Optional)"), Tooltip("The text mesh that will be used to display the fixed Gate Text (OutputText)")]
        public TMP_Text FixedOutputText;

        [BigHeader("Upgrade Settings")]
        [Tooltip("If Enabled, GateText output will be affected by other objects in runtime\nFixed Output will not be affected")]
        public bool EnableUpgrade;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("The objects layer that will affect/upgrade the gate")]
        public LayerMask TriggerLayers;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("The objects Tag that will affect/upgrade the gate")]
        public string[] TriggerTags;
        [Line]
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("Added value per trigger\nPositive or negative")]
        public float IncreaseAmount;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("For controlling the spamming upgrades, this will give a cooldown between each trigger")]
        [Min(0)]public float IncreaseCooldown;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("If enabled, the collider object will be destroyed after upgrade")]
        public bool DestroyColliderOnHit = true;
        [Line]
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("Events when the gate output stands between positive and negative (output = 0)")]
        public UnityEvent OnZeroEvent;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), InnerHint("(Optional)"), Tooltip("Spawned effect/object when zero event triggered")]
        public GameObject OnZeroEventSpawn;
        [ReadOnlyIf(nameof(EnableFixedOutput), false), Tooltip("Position offset of the spawned effect/object")]
        public Vector3 SpawnOffset;

        [BigHeader("Pair Gates")]
        [InnerHint("(Optional)"), Tooltip("Two gates connected togther, if one collected, the other will instantely collected")]
        public MOST_Gate PairedGate;

        [BigHeader("Animations")]
        [InnerHint("(Optional)")] public Animation Animation;
        public string OnUpgradeAnimation;
        public string OnCollectAnimation;

        [BigHeader("Effects")]
        [InnerHint("(Optional)"), Tooltip("Spawned effect/object when the gate collected")]
        public GameObject OnCollectSpawn;
        [Tooltip("As Projectile Movement, When Jump toward enabled")]
        [Min(0)] public float HitTime;
        [Tooltip("As Projectile Movement, When Jump toward enabled")]
        [Min(0)] public float MaxHight;

        GameObject _activeModel;
        float _cooldown;

#if UNITY_EDITOR
        void OnValidate() { UnityEditor.EditorApplication.delayCall += Validate; }
#endif
        void Awake() { Validate(); }
        void Update() { _cooldown -= Time.deltaTime; }
        void Validate()
        {
            if (PositiveModel) PositiveModel.SetActive(false);
            if (NegativeModel) NegativeModel.SetActive(false);
            if (CustomModel) CustomModel.SetActive(false);

            if (NegativeModel && (FixedOutputControlGates ? (OutputText.StartsWith("-") || OutputText.StartsWith("÷") || OutputText == "False") :
                (GateText.StartsWith("-") || GateText.StartsWith("÷") || GateText == "False")))
            {
                NegativeModel.SetActive(true);
                _activeModel = NegativeModel;
            }
            else if (PositiveModel && (FixedOutputControlGates ? (OutputText.StartsWith("x") || OutputText.StartsWith("+") || OutputText == "True") :
                (GateText.StartsWith("x") || GateText.StartsWith("+") || GateText == "True")))
            {
                PositiveModel.SetActive(true);
                _activeModel = PositiveModel;
            }
            else if (CustomModel)
            {
                CustomModel.SetActive(true);
                _activeModel = CustomModel;
            }

            if (GatePost) GatePost.text = HidePositiveSign && GateText.StartsWith("+") ? GateText[1..] : GateText;
            if(FixedOutputText) FixedOutputText.text = OutputText;
        }

        void OnTriggerEnter(Collider collider)
        {
            if (IsCollected || !EnableUpgrade) return;
            bool compareLayerAndTag = false;
            
            // Compare the collider tag to each tag in DamageTags
            foreach (string tag in TriggerTags) if (collider.gameObject.CompareTag(tag)) compareLayerAndTag = true; // Check tags
            if (!compareLayerAndTag) compareLayerAndTag = TriggerLayers == (TriggerLayers | (1 << collider.gameObject.layer)); // Check layers if tags not match

            if (compareLayerAndTag)
            {
                if (DestroyColliderOnHit) Destroy(collider.gameObject);
                if (_cooldown > 0) return;
                if ((float.TryParse(GateText[1..], out float num) || GateText[1..] == "0" )&& (GateText.StartsWith("-") ||
                    GateText.StartsWith("+") || GateText.StartsWith("÷") || GateText.StartsWith("x")))
                {
                    _cooldown = IncreaseCooldown;
                    if (GateText.StartsWith("-") || GateText.StartsWith("÷")) num -= IncreaseAmount;
                    else if (GateText.StartsWith("+") || GateText.StartsWith("x")) num += IncreaseAmount;
                    if (num <= 0)
                    {
                        GateText = (GateText.StartsWith("-") ? "+" : GateText.StartsWith("+") ? "-" :
                                   GateText.StartsWith("x") ? "÷" : "x" ) + (num == 0? "0" : num.ToString());
                        OnZeroEvent?.Invoke();
                        if(OnZeroEventSpawn) Destroy(Instantiate(OnZeroEventSpawn, transform.position + SpawnOffset, Quaternion.identity),5);
                        Validate();
                    }
                    else
                    {
                        GateText = GateText[0] + num.ToString();
                    }
                    if (GatePost) GatePost.text = HidePositiveSign && GateText.StartsWith("+") ? GateText[1..] : GateText;
                    if (Animation && OnUpgradeAnimation != string.Empty) Animation.Play(OnUpgradeAnimation);
                }
                else Debug.Log("Gate Value Upgrade faild, Seems Gate text is not a number or not starts with a sign");
            }
        }

        public float Calculation(float score)
        {
            int Kcounter = 1;
            if (!IsCollected)
            {
                if (Animation && OnCollectAnimation != string.Empty) Animation.Play(OnCollectAnimation);
                if (PairedGate)
                {
                    PairedGate.IsCollected = true;
                    if (PairedGate.Animation && PairedGate.OnCollectAnimation != string.Empty)
                        PairedGate.Animation.Play(OnCollectAnimation);
                }
                GetComponent<Collider>().enabled = false; // if one gate triggered disable collision in both gates

                if (OnCollectSpawn) Destroy(Instantiate(OnCollectSpawn,transform.position, Quaternion.identity), 5);
                if (_activeModel && _activeModel.activeInHierarchy && _activeModel.GetComponent<AudioSource>())
                    _activeModel.GetComponent<AudioSource>().Play();
                IsCollected = true;
            }
            
            string GateText = EnableFixedOutput? OutputText: this.GateText;
            while (char.ToLower(GateText[^1]) == 'k') // each k at the end will be replaced by "000"
            {
                GateText = GateText.Remove(GateText.Length - 1); // Remove this last k
                Kcounter *= 1000;                                // and add 000 to Kcounter
            }
            float.TryParse(GateText[1..], out float num);

            if (GateText.StartsWith("-")) return score - num * Kcounter * AmountRescale;
            else if (GateText.StartsWith("+")) return score + num * Kcounter * AmountRescale;
            else if (GateText.StartsWith("x")) return score * num * Kcounter;
            else if (GateText.StartsWith("÷")) return score / (num * Kcounter);
            else return -1; // Green Gate
        }

        public float Calculation()
        {
            int Kcounter = 1;
            string GateText = EnableFixedOutput ? OutputText : this.GateText;
            while (char.ToLower(GateText[^1]) == 'k') // each k at the end will be replaced by "000"
            {
                GateText = GateText.Remove(GateText.Length - 1); // Remove this last k
                Kcounter *= 1000;                                // and add 000 to Kcounter
            }
            float.TryParse(GateText[1..], out float num);

            if (!IsCollected)
            {
                if (Animation && OnCollectAnimation != string.Empty) Animation.Play(OnCollectAnimation);
                if (PairedGate)
                {
                    PairedGate.IsCollected = true;
                    if (PairedGate.Animation && PairedGate.OnCollectAnimation != string.Empty)
                        PairedGate.Animation.Play(OnCollectAnimation);
                }
                GetComponent<Collider>().enabled = false; // if one gate triggered disable collision in both gates

                if (OnCollectSpawn) Destroy(Instantiate(OnCollectSpawn, transform.position, Quaternion.identity), 5);
                if (_activeModel && _activeModel.activeInHierarchy && _activeModel.GetComponent<AudioSource>())
                    _activeModel.GetComponent<AudioSource>().Play();

                if (Type == GateType.Currency) CurrencyDataHolder.Get<IntData>(CurrencyName).Add((int)num);
                IsCollected = true;
            }

            if (GateText.StartsWith("-")) return - num * Kcounter * AmountRescale;
            else if (GateText.StartsWith("+")) return num * Kcounter * AmountRescale;
            else if (GateText.StartsWith("x")) return num * Kcounter;
            else if (GateText.StartsWith("÷")) return num * Kcounter;
            else return -1; // Green Gate
        }

        public GateSign GetSign()
        {
            string GateText = EnableFixedOutput ? OutputText : this.GateText;
            if (GateText.StartsWith("-")) return GateSign.Minus;
            else if (GateText.StartsWith("+")) return GateSign.Plus;
            else if (GateText.StartsWith("x")) return GateSign.Multiply;
            else if (GateText.StartsWith("÷")) return GateSign.Subtract;
            else return GateSign.NoSign; // Green Gate
        }

        public void JumpToward()
        {
            gameObject.AddComponent<Projectile_Movement>();
            GetComponent<Projectile_Movement>().UseGravityAtEnd = false;
            GetComponent<Projectile_Movement>().HitTime = HitTime;
            GetComponent<Projectile_Movement>().MaxHeight = MaxHight + transform.position.y;
            GetComponent<Projectile_Movement>().RandomRotationSpeed = 0;
            GetComponent<Projectile_Movement>().Activate(GameObject.FindGameObjectWithTag("Character").transform);
        }

        public void DestroyGate()
        {
            Destroy(gameObject);
        }
    }
}