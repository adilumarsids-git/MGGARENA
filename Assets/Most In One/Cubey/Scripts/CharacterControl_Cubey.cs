using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, DisallowMultipleComponent]
    public class CharacterControl_Cubey : MonoBehaviour
    {
        [BigHeader("Control Settings")]
        [Tooltip("The Behavior controls the game/level state, to send the current level state to it")]
        [ReadOnly] public LevelSettings_Cubey LevelSettings;

        [InnerHint("(Optional)"), Tooltip("UI Text used to display the current remain text")]
        public Text BlockRemainsText;
        [InnerHint("(Optional)"), Tooltip("in this behavior, a progress bar controller attached")]
        public GameObject ProgressBar;

        [BigHeader("Move Settings")]
        [Tooltip("enable or disable or check if this Touch control is enabled or not")]
        public bool EnableMove; // this for move controller to enable and disable it
        [Tooltip("Character movement Speed")]
        [Min(.1f)] public float Speed;
        [Tooltip("Touch move strength to enable swipe")]
        [Min(1f)] public float SwipeSinstivity;

        [BigHeader("Events")]
        public UnityEvent OnTriggerBlock;
        public UnityEvent OnStop;

        [BigHeader("Animations")]
        [Tooltip("The list of all texture animations for all states\ncheck the sorting in ChangeEmoji()")]
        public GameObject[] EmojiList; // 0 for idle // 1 for Moving Right // 2 for Left // 3 for Up // 4 For Down // 5 For Win // 6 for Lose

        List<GameObject> _activeBlocks = new();
        Vector3 fp, lp, _nextPosition;
        float _swipeDistanceX, _swipeDistanceY;
        string _direction; bool _move, _mouseDown;

        void Start()
        {
            LevelSettings = FindFirstObjectByType<LevelSettings_Cubey>();
            transform.position = LevelSettings.StartBlock.transform.position;
            if(BlockRemainsText) BlockRemainsText.text = LevelSettings.BlockRemains.ToString();
        }
        public void EnableControl(bool enable)
        {
            EnableMove = enable;
        }
        void Update()
        {
            if (!EnableMove) return;
            HandleTouchSwipe();
            HandleMouseSwipe(); // <-- new
            if (_move) transform.position = Vector3.MoveTowards(transform.position, _nextPosition, Speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, _nextPosition) < 0.2f && _move) // reach the next point // Stop Character
            {
                ChangeEmoji("Idle");
                transform.position = _nextPosition;
                _move = false;
                OnStop?.Invoke();
            }
        }

        void HandleTouchSwipe()
        {
            foreach (Touch touch in Input.touches) // Swipe Controller
            {
                if (_move) break; // if Character is Moving... Stop Touch Control

                if (touch.phase == TouchPhase.Began)
                {
                    fp = touch.position;
                    lp = touch.position;
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    lp = touch.position;
                    _swipeDistanceX = Mathf.Abs(lp.x - fp.x);
                    _swipeDistanceY = Mathf.Abs(lp.y - fp.y);
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    TryCommitSwipe(fp, lp);
                }
            }
        }

        void HandleMouseSwipe()
        {
            if (_move) return;

            if (Input.GetMouseButtonDown(0))
            {
                _mouseDown = true;
                fp = Input.mousePosition;
                lp = fp;
            }
            else if (_mouseDown && Input.GetMouseButton(0))
            {
                lp = (Vector2)Input.mousePosition;
                _swipeDistanceX = Mathf.Abs(lp.x - fp.x);
                _swipeDistanceY = Mathf.Abs(lp.y - fp.y);
            }
            else if (_mouseDown && Input.GetMouseButtonUp(0))
            {
                _mouseDown = false;
                TryCommitSwipe(fp, lp);
            }
        }

        void TryCommitSwipe(Vector2 fp, Vector2 lp)
        {
            // keep your original angle math (screen-space)
            float angle = Mathf.Atan2(lp.x - fp.x, lp.y - fp.y) * 180f / Mathf.PI;
            Vector3 X_Z_Offset = Vector3.zero; // next target step

            // thresholds identical to touch
            float minX = SwipeSinstivity * 10f;
            float minY = SwipeSinstivity * 10f;

            if (angle > 45f && angle < 135f && _swipeDistanceX > minX)        // right
            {
                _direction = "right";
                X_Z_Offset.x += LevelSettings.BlockScale;
            }
            else if ((angle > 135f || angle < -135f) && _swipeDistanceY > minY) // down
            {
                _direction = "down";
                X_Z_Offset.z -= LevelSettings.BlockScale;
            }
            else if (angle < -45f && angle > -135f && _swipeDistanceX > minX) // left
            {
                _direction = "left";
                X_Z_Offset.x -= LevelSettings.BlockScale;
            }
            else if (angle > -45f && angle < 45f && _swipeDistanceY > minY)   // up
            {
                _direction = "up";
                X_Z_Offset.z += LevelSettings.BlockScale;
            }
            else
            {
                return; // no valid swipe
            }

            // your original stepwise forward-check with raycasts
            _nextPosition = transform.position;
            while (true)
            {
                Ray ray = new Ray(_nextPosition + X_Z_Offset + Vector3.up * 3f, transform.TransformDirection(Vector3.down));
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // layer 10 or a "Score Object" not yet visited allows moving forward
                    if ((hit.collider.CompareTag("Score Object") && !_activeBlocks.Contains(hit.collider.gameObject))
                         || hit.collider.gameObject.layer == 10)
                    {
                        _move = true;
                        _nextPosition += X_Z_Offset;

                        if (hit.collider.gameObject.layer == 10) // checkpoint — stop extending
                        {
                            var col = hit.collider.GetComponent<Collider>();
                            if (col) col.enabled = false;
                            break;
                        }
                    }
                    else break; // blocked — stop extending
                }
                else break;     // nothing hit — stop extending
            }

            if (_move) ChangeEmoji("Moving");
        }

        public void BlockCreated() // this called from the block object when it's triggerd by character
        {
            LevelSettings.BlockRemains--;
            if(BlockRemainsText) BlockRemainsText.text = LevelSettings.BlockRemains.ToString();
            if (ProgressBar)ProgressBar.transform.localPosition += Vector3.right * LevelSettings.ProgressBarSteps;
            LevelSettings.PercentageCompleted = 100 * LevelSettings.BlockRemains / (_activeBlocks.Count + LevelSettings.BlockRemains);
            OnTriggerBlock?.Invoke();
        }

        public void GameOver() // When game over (Reach end point)
        {
            EnableMove = false;
            if (LevelSettings.BlockRemains <= 0) // Win
            {
                ChangeEmoji("Win");
                FindFirstObjectByType<UniversalGameManager>().OnWin();
            }
            else // Lose
            {
                FindFirstObjectByType<UniversalGameManager>().OnLose();
                ChangeEmoji("Lost");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Score Object"))
            {
                if (_activeBlocks.Contains(other.gameObject)) return;
                if (other.gameObject == LevelSettings.EndBlock)
                {
                    transform.position = other.gameObject.transform.position; // because when end block triggerd the movement system diabled
                    BlockCreated(); GameOver();
                    _activeBlocks.Add(other.gameObject);
                    if(other.gameObject.GetComponent<MOST_Action>())
                        other.gameObject.GetComponent<MOST_Action>().PlayAllActions();
                }
                else if (other.gameObject != LevelSettings.StartBlock) BlockCreated();
            }
        }
        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Score Object")) // when character leave the square... activate the block and move it
            {
                _activeBlocks.Add(other.gameObject);
                if (other.gameObject.GetComponent<MOST_Action>())
                    other.gameObject.GetComponent<MOST_Action>().PlayAllActions();
            }
        }

        void ChangeEmoji(string state)
        {
            foreach (GameObject em in EmojiList) em.SetActive(false);
            if (state == "Moving") EmojiList[_direction == "right" ? 1 : _direction == "left" ? 2 : _direction == "up" ? 3 : 4].SetActive(true);
            else if (state == "Win") EmojiList[5].SetActive(true);
            else if (state == "Lost") EmojiList[6].SetActive(true);
            else EmojiList[0].SetActive(true);
        }
    }
}
