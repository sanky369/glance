using System;
using System.Reflection;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

namespace Glance.Editor
{
    [InitializeOnLoad]
    public static class GlancePointerCommands
    {
        private static Mouse mouse;
        private static Mouse previousMouse;
        private static InputSettings originalSettings;
        private static InputSettings sessionSettings;
        private static InputSystemUIInputModule uiModule;
        private static InputActionAsset originalActions;
        private static InputActionAsset sessionActions;
        private static bool busy;
        private static bool pressed;
        private static bool hovered;
        private static Vector2 start, end, position;
        private static double started;
        private static float duration;
        private static int queuedFrame;
        private static bool releasing;

        static GlancePointerCommands()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
            };
        }

        [CliGroupCommand("glance.pointer", "Start an isolated virtual mouse session in Play mode.")]
        public static object Begin()
        {
            RequirePlaying();
            if (mouse != null) return Status();
            uiModule = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            if (uiModule == null || uiModule.actionsAsset == null)
                throw new InvalidOperationException("An active Input System UI module is required.");
            originalSettings = InputSystem.settings;
            // InputManager destroys HideAndDontSave settings when replacing them.
            if (originalSettings.hideFlags == HideFlags.HideAndDontSave)
                originalSettings = UnityEngine.Object.Instantiate(originalSettings);
            previousMouse = Mouse.current;
            sessionSettings = UnityEngine.Object.Instantiate(originalSettings);
            sessionSettings.name = "Glance pointer session (temporary)";
            sessionSettings.hideFlags = HideFlags.HideAndDontSave;
            sessionSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            sessionSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = sessionSettings;
            mouse = InputSystem.AddDevice<Mouse>("GlancePointerMouse");
            originalActions = uiModule.actionsAsset;
            sessionActions = UnityEngine.Object.Instantiate(originalActions);
            sessionActions.hideFlags = HideFlags.HideAndDontSave;
            sessionActions.devices = new InputDevice[] { mouse };
            uiModule.actionsAsset = sessionActions;
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            return Status();
        }

        [CliGroupCommand("glance.pointer", "Queue a click or drag in screenshot coordinates (top-left origin).")]
        public static object Gesture(
            [CliArg("x", "Start X in screenshot pixels.", Required = true)] float x,
            [CliArg("y", "Start Y from the screenshot top edge.", Required = true)] float y,
            [CliArg("end_x", "End X in screenshot pixels.", Required = true)] float endX,
            [CliArg("end_y", "End Y from the screenshot top edge.", Required = true)] float endY,
            [CliArg("width", "Screenshot width in pixels.", Required = true)] int width,
            [CliArg("height", "Screenshot height in pixels.", Required = true)] int height,
            [CliArg("duration", "Gesture duration in seconds, 0.05 to 2.")] float seconds = 0.12f)
        {
            RequirePlaying();
            if (mouse == null) throw new InvalidOperationException("Run glance.pointer begin first.");
            if (busy) throw new InvalidOperationException("A pointer gesture is still running.");
            if (width < 1 || height < 1 || !Finite(seconds) || seconds < .05f || seconds > 2f)
                throw new ArgumentException("Use positive image dimensions and a duration from 0.05 to 2 seconds.");
            ValidatePoint(x, y, width, height);
            ValidatePoint(endX, endY, width, height);
            var size = ViewSize();
            start = new Vector2(x / width * size.x, (1f - y / height) * size.y);
            end = new Vector2(endX / width * size.x, (1f - endY / height) * size.y);
            position = start;
            duration = seconds;
            pressed = false;
            hovered = false;
            releasing = false;
            busy = true;
            return new { queued = true, targetWidth = size.x, targetHeight = size.y,
                start = new { x = start.x, y = start.y }, end = new { x = end.x, y = end.y } };
        }

        [CliGroupCommand("glance.pointer", "Read pointer progress; completion means input delivery, not task success.")]
        public static object Status() => new { active = mouse != null, busy, pressed, queuedFrame,
            position = new { x = position.x, y = position.y } };

        [CliGroupCommand("glance.pointer", "Remove the virtual mouse and restore the original input settings.")]
        public static object End()
        {
            if (busy) throw new InvalidOperationException("Wait for the bounded gesture to finish before ending the session.");
            Cleanup();
            return Status();
        }

        private static void BeforeInputUpdate()
        {
            if (!busy || mouse == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (!hovered)
            {
                position = start;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                queuedFrame = Time.frameCount;
                hovered = true;
                return;
            }
            if (Time.frameCount <= queuedFrame) return;
            if (releasing)
            {
                if (Time.frameCount > queuedFrame) busy = false;
                return;
            }
            if (!pressed)
            {
                started = Time.realtimeSinceStartupAsDouble;
                pressed = true;
                position = start;
            }
            else
            {
                var progress = (float)((Time.realtimeSinceStartupAsDouble - started) / duration);
                position = Vector2.Lerp(start, end, Mathf.Clamp01(progress));
                if (progress >= 1f)
                {
                    pressed = false;
                    releasing = true;
                }
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });
            queuedFrame = Time.frameCount;
        }

        private static Vector2 ViewSize()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            var method = type?.GetMethod("GetMainPlayModeViewTargetSize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Cannot calibrate the Game view; refusing input.");
            var size = (Vector2)method.Invoke(null, null);
            if (size.x < 1 || size.y < 1) throw new InvalidOperationException("No valid Game view render target.");
            return size;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void ValidatePoint(float x, float y, int width, int height)
        {
            if (!Finite(x) || !Finite(y) || x < 0 || y < 0 || x >= width || y >= height)
                throw new ArgumentOutOfRangeException("Pointer coordinates must lie within the supplied screenshot.");
        }

        private static void RequirePlaying()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("The HMI must be playing.");
        }

        private static void Cleanup()
        {
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            if (uiModule != null && originalActions != null) uiModule.actionsAsset = originalActions;
            if (sessionActions != null) UnityEngine.Object.DestroyImmediate(sessionActions);
            sessionActions = originalActions = null;
            uiModule = null;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            mouse = null;
            if (originalSettings != null) InputSystem.settings = originalSettings;
            if (sessionSettings != null) UnityEngine.Object.DestroyImmediate(sessionSettings);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            originalSettings = sessionSettings = null;
            previousMouse = null;
            busy = pressed = hovered = releasing = false;
        }
    }
}
