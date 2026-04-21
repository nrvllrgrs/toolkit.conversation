using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;
using Yarn.Unity.Attributes;
using System.Linq;


#if USE_TMP
using TMPro;
#else
using TextMeshProUGUI = Yarn.Unity.TMPShim;
using TMP_Text = Yarn.Unity.TMPShim;
#endif

using InputSystemAvailability = Yarn.Unity.InputSystemAvailability;

#nullable enable

namespace ToolkitEngine.Dialogue
{
	/// <summary>
	/// A dialogue presenter that listens for user input and sends requests to a <see
	/// cref="DialogueRunner"/> to advance the presentation of the current line,
	/// either by asking a dialogue runner to hurry up its delivery, advance to
	/// the next line, or cancel the entire dialogue session.
	/// </summary>
	public sealed class DialogueLineAdvancer : DialoguePresenterBase, IActionMarkupHandler
	{
		/// <summary>
		/// The <see cref="DialogueRunner"/> that will receive requests to
		/// advance or cancel content. Populated automatically from
		/// <see cref="LocalizedLine.Source"/> when the first line is received,
		/// so no Inspector reference is required.
		/// </summary>
		private DialogueRunner? runner;

		/// <summary>
		/// The <see cref="DialoguePresenterBase"/> that this LineAdvancer should subscribe to for notifications that the line is fully visible.
		/// </summary>
		/// <remarks>When <see cref="RequestLineHurryUp"/> is called, if the line is fully visible, the <see cref="runner"/> object will have its <see cref="DialogueRunner.RequestNextLine"/> method called (instead of its <see cref="DialogueRunner.RequestHurryUpLine"/> method).
		/// This behaviour is only the case when the <see cref="separateHurryUpAndAdvanceControls"/> is set to false.
		///</remarks>
		[SerializeField] DialoguePresenterBase? presenter;

		/// <summary>
		/// Should this line advancer use different actions for hurrying up a line and advancing a line?
		/// </summary>
		/// <remarks>
		/// When this is false if the player requests a line to hurry up and the line is fully shown the <see cref="DialogueRunner.RequestNextLine"/> method will be called instead of the <see cref="DialogueRunner.RequestHurryUpLine"/> method.
		/// This behaviour is only the case when <see cref="presenter"/> is not null and the presenter is presenting it's line content via it's <see cref="DialoguePresenter.Typewriter"/> property.
		/// </remarks>
		[SerializeField] private bool separateHurryUpAndAdvanceControls = false;

		/// <summary>
		/// If <see langword="true"/>, repeatedly signalling that the line
		/// should be hurried up will cause the line advancer to request that
		/// the next line be shown.
		/// </summary>
		/// <seealso cref="advanceRequestsBeforeCancellingLine"/>
		[Space]
		[Tooltip("Does repeatedly requesting a line advance cancel the line?")]
		public bool multiAdvanceIsCancel = false;

		/// <summary>
		/// The number of times that a 'hurry up' signal occurs before the line
		/// advancer requests that the next line be shown.
		/// </summary>
		/// <seealso cref="multiAdvanceIsCancel"/>
		[ShowIf(nameof(multiAdvanceIsCancel))]
		[Indent]
		[Label("Advance Count")]
		[Tooltip("The number of times that a line advance occurs before the current line is cancelled.")]
		public int advanceRequestsBeforeCancellingLine = 2;

		/// <summary>
		/// The number of times that this object has received an indication that
		/// the line should be advanced.
		/// </summary>
		/// <remarks>
		/// This value is reset to zero when a new line is run. When the line is
		/// advanced, this value is incremented. If this value ever meets or
		/// exceeds <see cref="advanceRequestsBeforeCancellingLine"/>, the line
		/// will be cancelled.
		/// </remarks>
		private int numberOfAdvancesThisLine = 0;

		private static bool? s_inputSystemInstalled;
		private static bool? s_enableInputSystem;
		private static bool? s_enableLegacyInput;

		/// <summary>
		/// The type of input that this line advancer responds to.
		/// </summary>
		public enum InputMode
		{
			/// <summary>
			/// The line advancer responds to Input Actions from the <a
			/// href="https://docs.unity3d.com/Packages/com.unity.inputsystem@latest">Unity
			/// Input System</a>.
			/// </summary>
			InputActions,
			/// <summary>
			/// The line advancer responds to keypresses on the keyboard.
			/// </summary>
			KeyCodes,
			/// <summary>
			/// The line advancer does not respond to any input.
			/// </summary>
			/// <remarks>When a line advancer's <see cref="UsedInputMode"/> is set
			/// to <see cref="None"/>, call the <see
			/// cref="RequestLineHurryUp"/>, <see cref="RequestNextLine"/> and
			/// <see cref="RequestDialogueCancellation"/> methods directly from
			/// your code to control line advancement.</remarks>
			None,
			/// <summary>
			/// The line advancer responds to input from the legacy <a
			/// href="https://docs.unity3d.com/Manual/class-InputManager.html">Input
			/// Manager</a>.
			/// </summary>
			LegacyInputAxes,
		}

		/// <summary>
		/// The type of input that this line advancer responds to.
		/// </summary>
		/// <seealso cref="InputMode"/>
		[Tooltip("The type of input that this line advancer responds to.")]
		[Space]
		[MessageBox(sourceMethod: nameof(ValidateInputMode))]
		[SerializeField] InputMode inputMode;

		// when using the same input for different actions, for example using spacebar to select an option but also spacebar to hurry up lines
		// the action for hurrying up the line will happen the same frame as the action for selection
		// so if a line follows options (very common), that line might well get told to instantly hurry up
		// which isn't ideal, so this tracks the frame that content arrives and hurry up events cannot run the same frame as their content appears
		private int frameContentReceived = 0;

		InputMode UsedInputMode
		{
			get
			{
				bool inputSystemAvailable = enableInputSystem && inputSystemInstalled;
				if (inputMode == InputMode.InputActions && !inputSystemAvailable)
				{
					// We're configured to use input actions, but the input
					// system is not enabled. Fall back to key codes.
					return InputMode.KeyCodes;
				}
				else
				{
					return inputMode;
				}
			}
		}

		private static bool inputSystemInstalled
		{
			get
			{
				if (!s_inputSystemInstalled.HasValue)
				{
					s_inputSystemInstalled = ReadBool(nameof(inputSystemInstalled));
				}
				return s_inputSystemInstalled.Value;
			}
		}

		private static bool enableInputSystem
		{
			get
			{
				if (!s_enableInputSystem.HasValue)
				{
					s_enableInputSystem = ReadBool(nameof(enableInputSystem));
				}
				return s_enableInputSystem.Value;
			}
		}

		private static bool enableLegacyInput
		{
			get
			{
				if (!s_enableLegacyInput.HasValue)
				{
					s_enableLegacyInput = ReadBool(nameof(enableLegacyInput));
				}
				return s_enableLegacyInput.Value;
			}
		}

		private static bool ReadBool(string fieldName)
		{
			var field = typeof(InputSystemAvailability).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
			if (field == null)
			{
				return false;
			}
			return (bool)field.GetValue(null);
		}

		/// <summary>
		/// Validates the current value of <see cref="inputMode"/>, and
		/// potentially returns a message box to display.
		/// </summary>
		private MessageBoxAttribute.Message ValidateInputMode()
		{
#pragma warning disable CS0162 // Unreachable code detected
			if (this.inputMode == InputMode.None)
			{
				return MessageBoxAttribute.Info($"To use this component, call the following methods on it:\n\n" +
					$"- {nameof(this.RequestLineHurryUp)}()\n" +
					$"- {nameof(this.RequestNextLine)}()\n" +
					$"- {nameof(this.RequestOptionHurryUp)}()\n" +
					$"- {nameof(this.RequestDialogueCancellation)}()"
				);
			}

			if (this.inputMode == InputMode.LegacyInputAxes && !enableLegacyInput)
			{
				return MessageBoxAttribute.Warning("The Input Manager (Old) system is not enabled.\n\nEither change this setting to Input Actions, or enable Input Manager (Old) in Project Settings > Player > Configuration > Active Input Handling.");
			}

			if (this.inputMode == InputMode.InputActions)
			{
				if (inputSystemInstalled == false)
				{
					return MessageBoxAttribute.Warning("Please install the Unity Input System package to use Input Actions.\n\nFalling back to the keyboard in the meantime.");
				}
				if (!enableInputSystem)
				{
					return MessageBoxAttribute.Warning("The Unity Input System is not enabled.\n\nEither change this setting, or enable Input System in Project Settings > Player > Configuration > Active Input Handling.\n\nFalling back to the keyboard in the meantime.");
				}
			}

			return MessageBoxAttribute.NoMessage;
#pragma warning restore CS0162 // Unreachable code detected
		}

#if USE_INPUTSYSTEM
		/// <summary>
		/// The Input Action that triggers a request to advance to the next
		/// piece of content.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.InputActions)]
		[Indent]
		[SerializeField] UnityEngine.InputSystem.InputActionReference? hurryUpLineAction;

		/// <summary>
		/// The Input Action that triggers an instruction to cancel the current
		/// line.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.InputActions)]
		[ShowIf(nameof(separateHurryUpAndAdvanceControls))]
		[Indent]
		[SerializeField] UnityEngine.InputSystem.InputActionReference? nextLineAction;

		/// <summary>
		/// The Input Action that triggers an instruction to hurry up presenting the current options
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.InputActions)]
		[Indent]
		[SerializeField] UnityEngine.InputSystem.InputActionReference? hurryUpOptionsAction;

		/// <summary>
		/// The Input Action that triggers an instruction to cancel the entire
		/// dialogue.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.InputActions)]
		[Indent]
		[SerializeField] UnityEngine.InputSystem.InputActionReference? cancelDialogueAction;

		/// <summary>
		/// If true, the <see cref="hurryUpLineAction"/>, <see
		/// cref="nextLineAction"/> and <see cref="cancelDialogueAction"/> Input
		/// Actions will be enabled when the the dialogue runner signals that a
		/// line is running.
		/// </summary>
		[Tooltip("If true, the input actions above will be enabled when a line begins.")]
		[ShowIf(nameof(UsedInputMode), InputMode.InputActions)]
		[Indent]
		[SerializeField] bool enableActions = true;
#endif
		/// <summary>
		/// The legacy Input Axis that triggers a request to advance to the next
		/// piece of content.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.LegacyInputAxes)]
		[Indent]
		[SerializeField] string? hurryUpLineAxis = "Jump";

		/// <summary>
		/// The legacy Input Axis that triggers an instruction to cancel the
		/// current line.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.LegacyInputAxes)]
		[ShowIf(nameof(separateHurryUpAndAdvanceControls))]
		[Indent]
		[SerializeField] string? nextLineAxis = "Cancel";

		/// <summary>
		/// The legacy Input Axis that triggers an instruction to hurry up presenting the current options
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.LegacyInputAxes)]
		[Indent]
		[SerializeField] string? hurryUpOptionsAxis = "Jump";

		/// <summary>
		/// The legacy Input Axis that triggers an instruction to cancel the
		/// entire dialogue.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.LegacyInputAxes)]
		[Indent]
		[SerializeField] string? cancelDialogueAxis = "";

		/// <summary>
		/// The <see cref="KeyCode"/> that triggers a request to advance to the
		/// next piece of content.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.KeyCodes)]
		[Indent]
		[SerializeField] KeyCode hurryUpLineKeyCode = KeyCode.Space;

		/// <summary>
		/// The <see cref="KeyCode"/> that triggers an instruction to cancel the
		/// current line.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.KeyCodes)]
		[ShowIf(nameof(separateHurryUpAndAdvanceControls))]
		[Indent]
		[SerializeField] KeyCode nextLineKeyCode = KeyCode.Escape;

		/// <summary>
		/// The <see cref="KeyCode"/> that triggers an instruction to hurry up presenting options
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.KeyCodes)]
		[Indent]
		[SerializeField] KeyCode hurryUpOptionsKeyCode = KeyCode.Space;

		/// <summary>
		/// The <see cref="KeyCode"/> that triggers an instruction to cancel the
		/// entire dialogue.
		/// </summary>
		[ShowIf(nameof(UsedInputMode), InputMode.KeyCodes)]
		[Indent]
		[SerializeField] KeyCode cancelDialogueKeyCode = KeyCode.None;

#if USE_INPUTSYSTEM
		private void OnHurryUpLinePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
		{
			RequestLineHurryUpInternal();
		}
		private void OnHurryUpOptionsPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
		{
			RequestOptionHurryUp();
		}

		private void OnNextLinePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
		{
			RequestNextLine();
		}
		private void OnCancelDialoguePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
		{
			RequestDialogueCancellation();
		}
#endif
		// used to track the status of the presentation
		// you can think of this as a variation on multiple presses to advance a line
		// where if the presenter is awaiting input it is reasonable that pressing hurry up would advance to the next piece of content
		// but the default presenters can't really tell that apart
		// so the line advancer instead will handle this
		// this only works if the line advancer is added as a processor onto the presenters typewriter
		// but that is ok as that is the default
		// as people replace those defaults with more complex views and presenters they will also want to replace the line advancer anyways
		private enum PresentationStatus
		{
			Unknown, LineBegan, LineWaiting, OptionsBegan, OptionsWaiting,
		}
		private PresentationStatus status = PresentationStatus.Unknown;

		/// <summary>
		/// The runner for which <see cref="SetupRunner"/> has already executed.
		/// Used to ensure setup is repeated when the source runner changes, but
		/// never duplicated for the same runner.
		/// </summary>
		private DialogueRunner? setupCompletedForRunner = null;

		private void Start()
		{
			// Perform the presenter/typewriter setup that does not require
			// knowing the runner. The runner-dependent part (adding this
			// component to runner.DialoguePresenters) is deferred to
			// SetupRunner(), called from RunLineAsync once the runner is known
			// via LocalizedLine.Source.
			if (presenter == null || separateHurryUpAndAdvanceControls)
			{
				return;
			}

			presenter.Typewriter?.ActionMarkupHandlers.Add(this);

			// Null out the separate advance inputs so that a single control
			// handles both hurry-up and next-line based on presentation state.
			nextLineAxis = null;
			nextLineKeyCode = KeyCode.None;
#if USE_INPUTSYSTEM
			nextLineAction = null;
#endif
		}

		/// <summary>
		/// Performs one-time runner-dependent setup: adds this component to the
		/// runner's <see cref="DialogueRunner.DialoguePresenters"/> list so it
		/// continues to receive line and option callbacks.
		/// </summary>
		/// <param name="dialogueRunner">The runner obtained from
		/// <see cref="LocalizedLine.Source"/>.</param>
		private void SetupRunner(DialogueRunner dialogueRunner)
		{
			if (setupCompletedForRunner == dialogueRunner)
			{
				return;
			}
			setupCompletedForRunner = dialogueRunner;

			if (presenter == null || separateHurryUpAndAdvanceControls)
			{
				return;
			}

			// Only add ourselves if not already in the presenter list.
			// Reassigning DialoguePresenters triggers its setter, which
			// reinitialises all presenters and resets their Typewriter property
			// to null — causing every subsequent line to fall back to
			// InstantTypewriter and breaking the typewriter effect permanently.
			if (dialogueRunner.DialoguePresenters.Contains(this))
			{
				return;
			}

			var listOfPresenters = new List<DialoguePresenterBase?>(dialogueRunner.DialoguePresenters)
			{
				this
			};
			dialogueRunner.DialoguePresenters = listOfPresenters;
		}

		/// <summary>
		/// Called by a dialogue runner when dialogue starts to add input action
		/// handlers for advancing the line.
		/// </summary>
		/// <returns>A completed task.</returns>
		public override YarnTask OnDialogueStartedAsync()
		{
#if USE_INPUTSYSTEM
			if (enableActions)
			{
				if (hurryUpLineAction != null) { hurryUpLineAction.action.Enable(); }
				if (hurryUpOptionsAction != null) { hurryUpOptionsAction.action.Enable(); }
				if (nextLineAction != null) { nextLineAction.action.Enable(); }
				if (cancelDialogueAction != null) { cancelDialogueAction.action.Enable(); }
			}

			if (UsedInputMode == InputMode.InputActions)
			{
				// If we're using the input system, register callbacks to run
				// when our actions are performed.
				if (hurryUpLineAction != null) { hurryUpLineAction.action.performed += OnHurryUpLinePerformed; }
				if (hurryUpOptionsAction != null) { hurryUpOptionsAction.action.performed += OnHurryUpOptionsPerformed; }
				if (nextLineAction != null) { nextLineAction.action.performed += OnNextLinePerformed; }
				if (cancelDialogueAction != null) { cancelDialogueAction.action.performed += OnCancelDialoguePerformed; }
			}
#endif

			ResetLineTracking();
			return YarnTask.CompletedTask;
		}

		/// <summary>
		/// Called by a dialogue runner when dialogue ends to remove the input
		/// action handlers.
		/// </summary>
		/// <returns>A completed task.</returns>
		public override YarnTask OnDialogueCompleteAsync()
		{
#if USE_INPUTSYSTEM
			// If we're using the input system, remove the callbacks.
			if (UsedInputMode == InputMode.InputActions)
			{
				if (hurryUpLineAction != null) { hurryUpLineAction.action.performed -= OnHurryUpLinePerformed; }
				if (hurryUpOptionsAction != null) { hurryUpOptionsAction.action.performed -= OnHurryUpOptionsPerformed; }
				if (nextLineAction != null) { nextLineAction.action.performed -= OnNextLinePerformed; }
				if (cancelDialogueAction != null) { cancelDialogueAction.action.performed -= OnCancelDialoguePerformed; }
			}
#endif

			ResetLineTracking();
			return YarnTask.CompletedTask;
		}

		/// <summary>
		/// Called by a dialogue presenter to signal that a line is running.
		/// </summary>
		/// <inheritdoc cref="LinePresenter.RunLineAsync" path="/param"/>
		/// <returns>A completed task.</returns>
		public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
		{
			// Obtain the dialogue runner from the line's Source field and
			// perform any one-time runner-dependent setup.
			if (line.Source is DialogueRunner sourceRunner)
			{
				runner = sourceRunner;
				SetupRunner(sourceRunner);
			}
			else if (line.Source != null)
			{
				Debug.LogWarning($"{nameof(DialogueLineAdvancer)}: {nameof(LocalizedLine.Source)} is not a {nameof(DialogueRunner)} (got {line.Source.GetType().Name}). Request methods will not work.", this);
			}
			else
			{
				Debug.LogWarning($"{nameof(DialogueLineAdvancer)}: {nameof(LocalizedLine.Source)} is null. Request methods will not work.", this);
			}

			// A new line has come in, so reset the number of times we've seen a
			// request to skip.
			ResetLineTracking();
			status = PresentationStatus.LineBegan;

			frameContentReceived = Time.frameCount;

			// Defensively re-register with the presenter's current typewriter.
			// If DialoguePresenters was ever reassigned (e.g. by SetupRunner on a
			// different runner), the typewriter may have been replaced and our
			// Start() registration would have been lost.
			if (presenter != null && !separateHurryUpAndAdvanceControls)
			{
				var handlers = presenter.Typewriter?.ActionMarkupHandlers;
				if (handlers != null && !handlers.Contains(this))
				{
					handlers.Add(this);
				}
			}

			return YarnTask.CompletedTask;
		}

		/// <summary>
		/// Called by a dialogue presenter to signal that options are running.
		/// </summary>
		/// <inheritdoc cref="LinePresenter.RunOptionsAsync" path="/param"/>
		/// <returns>A completed task indicating that no option was selected by
		/// this view.</returns>
		public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
		{
			ResetLineTracking();
			status = PresentationStatus.OptionsBegan;

			frameContentReceived = Time.frameCount;

			return DialogueRunner.NoOptionSelected;
		}

		private void ResetLineTracking()
		{
			numberOfAdvancesThisLine = 0;
			status = PresentationStatus.Unknown;
		}


		private void RequestLineHurryUpInternal()
		{
			if (frameContentReceived == Time.frameCount)
			{
				return;
			}

			// in this mode we NEED to be in a state where a line showing, regardless of it's completion state
			if (!separateHurryUpAndAdvanceControls)
			{
				if (!(status == PresentationStatus.LineBegan || status == PresentationStatus.LineWaiting))
				{
					return;
				}
			}

			// Increment our counter of line advancements, and depending on the
			// new count, request that the runner 'soft-cancel' the line or
			// cancel the entire line
			// this is true regardless of if we are the hurry up mode or not

			numberOfAdvancesThisLine += 1;

			if (multiAdvanceIsCancel && numberOfAdvancesThisLine >= advanceRequestsBeforeCancellingLine)
			{
				RequestNextLine();
			}
			else
			{
				// at this stage we want to hurry up if we are in multiAdvanceIsCancel
				// and either hurry up or skip the line depending on the state 
				if (separateHurryUpAndAdvanceControls)
				{
					if (runner != null)
					{
						runner.RequestHurryUpLine();
					}
					else
					{
						Debug.LogError($"{nameof(DialogueLineAdvancer)} dialogue runner is null. No line with a valid Source has been received yet", this);
					}
				}
				else
				{
					if (status == PresentationStatus.LineWaiting)
					{
						RequestNextLine();
					}
					else
					{
						if (runner != null)
						{
							runner.RequestHurryUpLine();
						}
						else
						{
							Debug.LogError($"{nameof(DialogueLineAdvancer)} dialogue runner is null. No line with a valid Source has been received yet", this);
						}
					}
				}
			}
		}
		/// <summary>
		/// Requests that the line be hurried up.
		/// </summary>
		/// <remarks>If this method has been called more times for a single line
		/// than <see cref="numberOfAdvancesThisLine"/>, this method requests
		/// that the dialogue runner proceed to the next line. Otherwise, it
		/// requests that the dialogue runner instruct all line views to hurry
		/// up their presentation of the current line.
		/// </remarks>
		public void RequestLineHurryUp()
		{
			// Increment our counter of line advancements, and depending on the
			// new count, request that the runner 'soft-cancel' the line or
			// cancel the entire line

			numberOfAdvancesThisLine += 1;

			if (multiAdvanceIsCancel && numberOfAdvancesThisLine >= advanceRequestsBeforeCancellingLine)
			{
				RequestNextLine();
			}
			else
			{
				if (runner != null)
				{
					runner.RequestHurryUpLine();
				}
				else
				{
					Debug.LogError($"{nameof(DialogueLineAdvancer)} dialogue runner is null � no line with a valid Source has been received yet", this);
				}
			}
		}

		public void RequestOptionHurryUp()
		{
			if (frameContentReceived == Time.frameCount)
			{
				return;
			}

			if (runner == null)
			{
				Debug.LogError($"Unable to hurry up options: {nameof(DialogueLineAdvancer)} has not yet received a line with a valid Source", this);
				return;
			}

			if (!separateHurryUpAndAdvanceControls)
			{
				if (status == PresentationStatus.OptionsBegan || status == PresentationStatus.OptionsWaiting)
				{
					runner.RequestHurryUpOption();
				}
			}
			else
			{
				runner.RequestHurryUpOption();
			}
		}

		/// <summary>
		/// Requests that the dialogue runner proceeds to the next line.
		/// </summary>
		public void RequestNextLine()
		{
			ResetLineTracking();
			if (runner != null)
			{
				runner.RequestNextLine();
			}
			else
			{
				Debug.LogError($"{nameof(DialogueLineAdvancer)} dialogue runner is null. No line with a valid Source has been received yet", this);
			}
		}

		/// <summary>
		/// Requests that the dialogue runner to instruct all line views to
		/// dismiss their content, and then stops the dialogue.
		/// </summary>
		public void RequestDialogueCancellation()
		{
			ResetLineTracking();
			// Stop the dialogue runner, which will cancel the current line as
			// well as the entire dialogue.
			if (runner != null)
			{
				runner.Stop().Forget();
			}
		}

		/// <summary>
		/// Called by Unity every frame to check to see if, depending on <see
		/// cref="UsedInputMode"/>, the <see cref="LineAdvancer"/> should take
		/// action.
		/// </summary>
		private void Update()
		{
			switch (UsedInputMode)
			{
				case InputMode.KeyCodes:
					if (InputSystemAvailability.GetKeyDown(hurryUpLineKeyCode)) { this.RequestLineHurryUpInternal(); }
					if (InputSystemAvailability.GetKeyDown(hurryUpOptionsKeyCode)) { this.RequestOptionHurryUp(); }
					if (InputSystemAvailability.GetKeyDown(nextLineKeyCode)) { this.RequestNextLine(); }
					if (InputSystemAvailability.GetKeyDown(cancelDialogueKeyCode)) { this.RequestDialogueCancellation(); }
					break;
				case InputMode.LegacyInputAxes:
					if (InputSystemAvailability.GetButtonDown(hurryUpLineAxis)) { this.RequestLineHurryUpInternal(); }
					if (InputSystemAvailability.GetButtonDown(hurryUpOptionsAxis)) { this.RequestOptionHurryUp(); }
					if (InputSystemAvailability.GetButtonDown(nextLineAxis)) { this.RequestNextLine(); }
					if (InputSystemAvailability.GetButtonDown(cancelDialogueAxis)) { this.RequestDialogueCancellation(); }
					break;
				default:
					// Nothing to do; 'None' takes no action, and 'InputActions'
					// doesn't poll in Update()
					break;
			}
		}

		public void OnPrepareForLine(MarkupParseResult line, TMP_Text text)
		{
			return;
		}

		public void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text)
		{
			return;
		}

		public YarnTask OnCharacterWillAppear(int currentCharacterIndex, MarkupParseResult line, CancellationToken cancellationToken)
		{
			return YarnTask.CompletedTask;
		}

		public void OnLineDisplayComplete()
		{
			if (status == PresentationStatus.LineBegan)
			{
				status = PresentationStatus.LineWaiting;
			}
			else if (status == PresentationStatus.OptionsBegan)
			{
				status = PresentationStatus.OptionsWaiting;
			}
		}

		public void OnLineWillDismiss()
		{
			return;
		}
	}
}