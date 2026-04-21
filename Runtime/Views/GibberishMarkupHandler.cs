using System.Threading;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

#nullable enable

#if USE_TMP
using TMPro;
#else
using TMP_Text = Yarn.Unity.TMPShim;
#endif

namespace ToolkitEngine.Dialogue
{
	/// <summary>
	/// An <see cref="IActionMarkupHandler"/> that plays gibberish audio clips
	/// as each character appears during typewriter delivery.
	/// </summary>
	/// <remarks>
	/// Attach this handler to a <see cref="LetterTypewriter"/>'s
	/// <c>ActionMarkupHandlers</c> list. Before each line is run, set
	/// <see cref="gibberishType"/> to the current speaker's gibberish profile
	/// (or <see langword="null"/> to silence playback for that line).
	/// </remarks>
	public class GibberishMarkupHandler : IActionMarkupHandler
	{
		/// <summary>
		/// The <see cref="audioSource"/> used to play gibberish clips.
		/// </summary>
		public AudioSource? audioSource { get; set; }

		/// <summary>
		/// The gibberish profile for the current speaker. Set this before each
		/// line begins. Set to <see langword="null"/> to disable audio for a
		/// line (e.g. narration with no speaker).
		/// </summary>
		public GibberishType? gibberishType { get; set; }

		// Tracks how many visible characters have appeared this line so
		// frequency gating is applied to the display sequence rather than
		// the raw character index (which may skip positions due to TMP tags).
		private int _visibleCharactersShown;

		/// <inheritdoc/>
		public void OnPrepareForLine(MarkupParseResult line, TMP_Text text)
		{
			_visibleCharactersShown = 0;
		}

		/// <inheritdoc/>
		public void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text)
		{
			_visibleCharactersShown = 0;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Plays a gibberish clip for every <c>n</c>th character, where
		/// <c>n</c> is <see cref="GibberishType.frequency"/>. The character
		/// at <paramref name="characterIndex"/> in the line's text is used as
		/// a hash source so that each character position produces a
		/// deterministic — but varied — clip and pitch.
		/// </remarks>
		public YarnTask OnCharacterWillAppear(int characterIndex, MarkupParseResult line, CancellationToken cancellationToken)
		{
			if (!cancellationToken.IsCancellationRequested
				&& gibberishType != null
				&& audioSource != null)
			{
				// Only play on every nth visible character per the frequency setting:
				//   frequency = 1 → every character
				//   frequency = 2 → every other character
				//   etc.
				if (_visibleCharactersShown % gibberishType.frequency == 0)
				{
					// Use the character at this position as the deterministic
					// hash source for clip/pitch selection. Fall back to a
					// space if the index is somehow out of range.
					char c = characterIndex < line.Text.Length
						? line.Text[characterIndex]
						: ' ';

					gibberishType.Play(audioSource, c);
				}

				_visibleCharactersShown++;
			}

			return YarnTask.CompletedTask;
		}

		/// <inheritdoc/>
		public void OnLineDisplayComplete() { }

		/// <inheritdoc/>
		public void OnLineWillDismiss() { }
	}
}