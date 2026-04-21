using System.Collections.Generic;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;
using Yarn.Unity.Attributes;

#nullable enable

#if USE_TMP
using TMPro;
#else
using TextMeshProUGUI = Yarn.Unity.TMPShim;
using TMP_Text = Yarn.Unity.TMPShim;
#endif

namespace ToolkitEngine.Dialogue
{
	public sealed class GibberishLinePresenter : DialoguePresenterBase
	{
		#region Fields

		/// <summary>
		/// The canvas group that contains the UI elements used by this Line
		/// View.
		/// </summary>
		/// <remarks>
		/// If <see cref="useFadeEffect"/> is true, then the alpha value of this
		/// <see cref="CanvasGroup"/> will be animated during line presentation
		/// and dismissal.
		/// </remarks>
		[Space]
		[MustNotBeNull]
		public CanvasGroup? canvasGroup;

		/// <summary>
		/// The <see cref="TMP_Text"/> object that displays the text of
		/// dialogue lines.
		/// </summary>
		[MustNotBeNull]
		public TMP_Text? lineText;

		/// <summary>
		/// Controls whether the <see cref="lineText"/> object will show the
		/// character name present in the line or not.
		/// </summary>
		/// <remarks>
		/// <para style="note">This value is only used if <see
		/// cref="characterNameText"/> is <see langword="null"/>.</para>
		/// <para>If this value is <see langword="true"/>, any character names
		/// present in a line will be shown in the <see cref="lineText"/>
		/// object.</para>
		/// <para>If this value is <see langword="false"/>, character names will
		/// not be shown in the <see cref="lineText"/> object.</para>
		/// </remarks>
		[Group("Character")]
		[Label("Shows Name In Line")]
		public bool showCharacterNameInLine = true;

		/// <summary>
		/// The <see cref="TMP_Text"/> object that displays the character
		/// names found in dialogue lines.
		/// </summary>
		/// <remarks>
		/// If the <see cref="LinePresenter"/> receives a line that does not contain
		/// a character name, this object will be left blank.
		/// </remarks>
		[Group("Character")]
		[Label("Name Field")]
		public TMP_Text? characterNameText = null;

		/// <summary>
		/// The game object that holds the <see cref="characterNameText"/> text
		/// field.
		/// </summary>
		/// <remarks>
		/// This is needed in situations where the character name is contained
		/// within an entirely different game object. Most of the time this will
		/// just be the same game object as <see cref="characterNameText"/>.
		/// </remarks>
		[Group("Character")]
		public GameObject? characterNameContainer = null;


		/// <summary>
		/// Controls whether the line view should fade in when lines appear, and
		/// fade out when lines disappear.
		/// </summary>
		/// <remarks><para>If this value is <see langword="true"/>, the <see
		/// cref="canvasGroup"/> object's alpha property will animate from 0 to
		/// 1 over the course of <see cref="fadeUpDuration"/> seconds when lines
		/// appear, and animate from 1 to zero over the course of <see
		/// cref="fadeDownDuration"/> seconds when lines disappear.</para>
		/// <para>If this value is <see langword="false"/>, the <see
		/// cref="canvasGroup"/> object will appear instantaneously.</para>
		/// </remarks>
		/// <seealso cref="canvasGroup"/>
		/// <seealso cref="fadeUpDuration"/>
		/// <seealso cref="fadeDownDuration"/>
		[Group("Fade")]
		[Label("Fade UI")]
		public bool useFadeEffect = true;

		/// <summary>
		/// The time that the fade effect will take to fade lines in.
		/// </summary>
		/// <remarks>This value is only used when <see cref="useFadeEffect"/> is
		/// <see langword="true"/>.</remarks>
		/// <seealso cref="useFadeEffect"/>
		[Group("Fade")]
		[ShowIf(nameof(useFadeEffect))]
		public float fadeUpDuration = 0.25f;

		/// <summary>
		/// The time that the fade effect will take to fade lines out.
		/// </summary>
		/// <remarks>This value is only used when <see cref="useFadeEffect"/> is
		/// <see langword="true"/>.</remarks>
		/// <seealso cref="useFadeEffect"/>
		[Group("Fade")]
		[ShowIf(nameof(useFadeEffect))]
		public float fadeDownDuration = 0.1f;

		/// <summary>
		/// Controls whether this Line View will automatically to the Dialogue
		/// Runner that the line is complete as soon as the line has finished
		/// appearing.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If this value is true, the Line View will 
		/// </para>
		/// <para style="note"><para>The <see cref="DialogueRunner"/> will not
		/// proceed to the next piece of content (e.g. the next line, or the
		/// next options) until all Dialogue Presenters have reported that they have
		/// finished presenting their lines. If a <see cref="LinePresenter"/>
		/// doesn't report that it's finished until it receives input, the <see
		/// cref="DialogueRunner"/> will end up pausing.</para>
		/// <para>
		/// This is useful for games in which you want the player to be able to
		/// read lines of dialogue at their own pace, and give them control over
		/// when to advance to the next line.</para></para>
		/// </remarks>
		[Group("Automatically Advance Dialogue")]
		public bool autoAdvance = false;

		/// <summary>
		/// The amount of time after the line finishes appearing before
		/// automatically ending the line, in seconds.
		/// </summary>
		/// <remarks>This value is only used when <see cref="autoAdvance"/> is
		/// <see langword="true"/>.</remarks>
		[Group("Automatically Advance Dialogue")]
		[ShowIf(nameof(autoAdvance))]
		[Label("Delay Before Advancing")]
		public float autoAdvanceDelay = 1f;

		/// <summary>
		/// The number of characters per second that should appear during a
		/// typewriter effect.
		/// </summary>
		[Group("Typewriter")]
		[Label("Letters per Second")]
		[Min(0)]
		public int lettersPerSecond = 60;

		/// <summary>
		/// A list of <see cref="ActionMarkupHandler"/> objects that will be
		/// used to handle markers in the line.
		/// </summary>
		[Group("Typewriter")]
		[Label("Event Handlers")]
		[UnityEngine.Serialization.FormerlySerializedAs("actionMarkupHandlers")]
		[SerializeField] List<ActionMarkupHandler> eventHandlers = new List<ActionMarkupHandler>();

		[Group("Audio")]
		public AudioSource audioSource;

		#endregion

		#region Properties

		/// <summary>
		/// The gibberish markup handler used to play per-character audio. Kept
		/// as a field so <see cref="RunLineAsync"/> can update its
		/// <see cref="GibberishMarkupHandler.gibberishType"/> before each line.
		/// </summary>
		private GibberishMarkupHandler _gibberishMarkupHandler = new();

		private List<IActionMarkupHandler> ActionMarkupHandlers
		{
			get
			{
				var pauser = new PauseEventProcessor();
				List<IActionMarkupHandler> handlers = new()
				{
					pauser,
					_gibberishMarkupHandler,
				};
				handlers.AddRange(eventHandlers);
				return handlers;
			}
		}

		#endregion

		#region Methods

		/// <inheritdoc/>
		public override YarnTask OnDialogueCompleteAsync()
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = 0;
			}
			return YarnTask.CompletedTask;
		}

		/// <inheritdoc/>
		public override YarnTask OnDialogueStartedAsync()
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = 0;
			}
			return YarnTask.CompletedTask;
		}

		/// <summary>
		/// Called by Unity on first frame.
		/// </summary>
		private void Awake()
		{
			if (characterNameContainer == null && characterNameText != null)
			{
				characterNameContainer = characterNameText.gameObject;
			}

			// Wire the audio source into the handler once at startup.
			_gibberishMarkupHandler.audioSource = audioSource;

			Typewriter = new LetterTypewriter()
			{
				ActionMarkupHandlers = ActionMarkupHandlers,
				Text = lineText,
				CharactersPerSecond = lettersPerSecond,
			};
		}

		/// <summary>Presents a line using the configured text view.</summary>
		/// <inheritdoc cref="DialoguePresenterBase.RunLineAsync(LocalizedLine, LineCancellationToken)" path="/param"/>
		/// <inheritdoc cref="DialoguePresenterBase.RunLineAsync(LocalizedLine, LineCancellationToken)" path="/returns"/>
		public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
		{
			if (lineText == null)
			{
				Debug.LogError($"{nameof(GibberishLinePresenter)} does not have a text view. Skipping line {line.TextID} (\"{line.RawText}\")");
				return;
			}

			// Resolve the current speaker's gibberish profile and hand it to
			// the handler before typewriter playback begins. If there is no
			// recognised speaker the handler will receive null and stay silent.
			if (DialogueManager.TryGetDialogueSpeakerTypeByCharacterName(line.CharacterName, out var speakerType))
			{
				_gibberishMarkupHandler.gibberishType = speakerType.gibberishType;
			}
			else
			{
				_gibberishMarkupHandler.gibberishType = null;
			}

			MarkupParseResult text;

			// Configure the text fields.
			if (characterNameText == null)
			{
				if (showCharacterNameInLine)
				{
					text = line.Text;
				}
				else
				{
					text = line.TextWithoutCharacterName;
				}
			}
			else
			{
				text = line.TextWithoutCharacterName;

				// We are configured to show character names in their own box,
				// but this line may not have one.
				if (characterNameContainer != null)
				{
					if (string.IsNullOrWhiteSpace(line.CharacterName))
					{
						characterNameContainer.SetActive(false);
					}
					else
					{
						characterNameContainer.SetActive(true);
						characterNameText.text = line.CharacterName;
					}
				}
			}

			Typewriter ??= new LetterTypewriter()
			{
				ActionMarkupHandlers = ActionMarkupHandlers,
				Text = lineText,
				CharactersPerSecond = lettersPerSecond,
			};

			Typewriter.PrepareForContent(text);

			if (canvasGroup != null)
			{
				if (useFadeEffect)
				{
					await Effects.FadeAlphaAsync(canvasGroup, 0, 1, fadeUpDuration, token.HurryUpToken);
				}
				else
				{
					canvasGroup.alpha = 1;
				}
			}

			await Typewriter.RunTypewriter(text, token.HurryUpToken).SuppressCancellationThrow();

			if (autoAdvance)
			{
				await YarnTask.Delay((int)(autoAdvanceDelay * 1000), token.NextContentToken).SuppressCancellationThrow();
			}
			else
			{
				await YarnTask.WaitUntilCanceled(token.NextContentToken).SuppressCancellationThrow();
			}

			Typewriter.ContentWillDismiss();

			if (canvasGroup != null)
			{
				if (useFadeEffect)
				{
					await Effects.FadeAlphaAsync(canvasGroup, 1, 0, fadeDownDuration, token.HurryUpToken).SuppressCancellationThrow();
				}
				else
				{
					canvasGroup.alpha = 0;
				}
			}
		}

		#endregion
	}
}