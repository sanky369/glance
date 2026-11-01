using UnityEngine;
using UnityEngine.Serialization;

public class BasicControllerInput : MonoBehaviour
{
    [SerializeField, Min(1f)] private float minRandomIdleVariantInterval = 5f;
    [SerializeField, Min(1f)] private float maxRandomIdleVariantInterval = 15f;
    [Space]
    [SerializeField, Min(0f)] private int numberOfIdleVariants = 2;
    [Space]
    [SerializeField] private Animator thisAnimator;

    private float _idleVariantTimer = 0f;

    private bool _isIdling = false;

    private void OnValidate()
    {
        if (!thisAnimator)
        {
            thisAnimator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        // --- Forward Input ---
        float vertical = 0f;
        float horizontal = 0f;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
        }

        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad != null)
        {
            var stick = gamepad.leftStick.ReadValue();
            vertical += stick.y;
            horizontal += stick.x;

            if (gamepad.dpad.up.isPressed) vertical += 1f;
            if (gamepad.dpad.down.isPressed) vertical -= 1f;
            if (gamepad.dpad.right.isPressed) horizontal += 1f;
            if (gamepad.dpad.left.isPressed) horizontal -= 1f;
        }

        vertical = Mathf.Clamp(vertical, -1f, 1f);
        horizontal = Mathf.Clamp(horizontal, -1f, 1f);

        var isWalking = vertical > 0.01f;
        thisAnimator.SetBool("IsWalking", isWalking);

        // --- Turning Input ---
        var turnDirection = horizontal;
        thisAnimator.SetFloat("TurnDirection", turnDirection);
        var isTurning = Mathf.Abs(turnDirection) > 0.01f;
        thisAnimator.SetBool("IsTurning", isTurning);

        // --- Idling (No Input) ---
        var newIsIdlingState = !isWalking && !isTurning;

        if (numberOfIdleVariants > 0)
        {
            // If the idling state changed
            if (_isIdling != newIsIdlingState)
            {
                _isIdling = newIsIdlingState;

                // If we just entered idling state
                if (_isIdling)
                {
                    // Pick a new random time until the next Idle Variant is played
                    _idleVariantTimer = Random.Range(minRandomIdleVariantInterval, maxRandomIdleVariantInterval);
                }
            }
            // If we are continuing in an idle state
            else if (_isIdling)
            {
                _idleVariantTimer -= Time.deltaTime;

                if (_idleVariantTimer <= 0f)
                {
                    thisAnimator.SetInteger("IdleVariant", Random.Range(0, numberOfIdleVariants));
                    thisAnimator.SetTrigger("PlayIdleVariant");

                    // Pick a new random time until the next Idle Variant is played
                    // Note: You may want to add additional padding time for the max
                    //       length of Idle Variant animation clips, but not required.
                    _idleVariantTimer = Random.Range(minRandomIdleVariantInterval, maxRandomIdleVariantInterval);
                }
            }
        }

        
    }
}