<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { useLayer } from '$lib/keys.svelte';
	import { t } from '$lib/i18n/index.svelte';
	/**
	 * The documentation. All of it. Two frames of a cat laughing at you, swapped a few times a
	 * second while the whole cat shakes, and held still for anyone who asked for less motion.
	 * Each frame is its lines of text, drawn as an SVG picture so it scales with the screen.
	 */
	const frames: string[][] = [
		[
			'            /\\                    /\\',
			'           /  \\__________________/  \\            HA!',
			'   HA!    /                          \\',
			'         |     ___            ___     |      HA!',
			'         |    /   \\          /   \\    |',
			"  =======(      ''            ''      )=======",
			'  =======(            .----.          )=======',
			'         |            \\(__)/          |',
			"          \\            '--'          /",
			"           '-.____________________.-'",
			'              /                  \\',
			'             /   .------------.   \\______________________',
			'            |   /              \\   ________________________)======-',
			'            |  |                |  |      (__)(__)(__)',
			'            |   \\              /   |',
			"             \\   '------------'   /                      <-- you",
			"              '.________________.'",
			'                (_____)    (_____)'
		],
		[
			'            /\\                    /\\        HA!  HA!',
			'           /  \\__________________/  \\',
			' HA!      /                          \\',
			'         |     ___            ___     |',
			'         |    /   \\          /   \\    |          HA!',
			"  =======(      ''            ''      )=======",
			'  =======(          .--------.        )=======',
			'         |          \\  (__)  /        |',
			"          \\          '------'        /",
			"           '-.____________________.-'",
			'              /                  \\',
			'             /   .------------.   \\______________________',
			'            |   /              \\   ________________________)========-',
			'            |  |                |  |      (__)(__)(__)',
			'            |   \\              /   |',
			"             \\   '------------'   /                      <-- you",
			"              '.________________.'",
			'                (_____)    (_____)'
		]
	];

	/** Every character cell is this wide and tall in the picture's own units. */
	const CELL_W = 6;
	const CELL_H = 11;
	const cols = Math.max(...frames.flat().map((l) => l.length)) + 6;
	const rows = Math.max(...frames.map((f) => f.length));

	let frame = $state(0);

	/** The frame on screen, with the pointer's caption in the current language. */
	const lines = $derived(
		frames[frame].map((l) => l.replace('<-- you', `<-- ${t('common.docsYou')}`))
	);

	// Escape goes back home, like the header's back link.
	useLayer(() => void goto(resolve('/')));

	$effect(() => {
		if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
		const timer = setInterval(() => (frame = (frame + 1) % frames.length), 380);
		return () => clearInterval(timer);
	});
</script>

<svelte:head><title>{t('common.docsTitle')} · YASS</title></svelte:head>

<main
	class="flex flex-1 flex-col items-center justify-center gap-5 px-4 py-10"
	data-testid="docs-page"
>
	<p
		class="relative max-w-[min(560px,100%)] rounded-[22px] bg-surface px-6 py-4 text-center text-[17px] font-extrabold tracking-[-0.01em] text-ink shadow-card sm:text-[20px]"
		data-testid="docs-quote"
	>
		{t('common.docsQuote')}
		<!-- The bubble's tail, pointing down at the cat doing the talking. -->
		<span
			class="absolute -bottom-2 left-1/2 h-4 w-4 -translate-x-1/2 rotate-45 bg-surface"
			aria-hidden="true"
		></span>
	</p>

	<!--
		A picture, not a block of text: the SVG scales to the screen as a whole, so the art keeps
		its shape on a phone. Each line is stretched to exactly its number of character cells
		(textLength), so the columns line up whatever monospace font the device has.
	-->
	<svg
		class="cat h-auto w-full max-w-[760px] text-ink-2"
		viewBox="0 0 {cols * CELL_W} {rows * CELL_H + 4}"
		role="img"
		aria-label={t('common.docsCat')}
		data-testid="docs-cat"
		data-frame={frame}
	>
		{#each lines as line, i (i)}
			{#if line.trim()}
				<text
					x="0"
					y={(i + 1) * CELL_H}
					font-size="10"
					font-weight="700"
					font-family="ui-monospace, Menlo, Consolas, monospace"
					fill="currentColor"
					textLength={line.length * CELL_W}
					lengthAdjust="spacingAndGlyphs">{line}</text
				>
			{/if}
		{/each}
	</svg>
</main>

<style>
	/* Shaking with laughter: a little hop and a wobble, from the feet. */
	.cat {
		transform-origin: 45% 95%;
		animation: cackle 0.38s ease-in-out infinite;
	}

	/* Leading spaces are part of the picture. */
	.cat text {
		white-space: pre;
	}

	@keyframes cackle {
		0%,
		100% {
			transform: translateY(0) rotate(0);
		}
		25% {
			transform: translateY(-3px) rotate(-1.5deg);
		}
		75% {
			transform: translateY(1px) rotate(1.5deg);
		}
	}
</style>
