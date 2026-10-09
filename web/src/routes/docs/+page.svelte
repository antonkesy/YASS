<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { useLayer } from '$lib/keys.svelte';
	import { t } from '$lib/i18n/index.svelte';
	/**
	 * The documentation. All of it. Two frames of a cat laughing at you, swapped a few times a
	 * second while the whole cat shakes, and held still for anyone who asked for less motion.
	 */
	const frames = [
		String.raw`
  HA HA!   /\_______/\
          /  >     <  \
         |  '       '  |
         |    .---.    |
         |    \___/    |
          \           /
           '-._____.-'___________
             |     |   ________ )==-
             |     |  (____)
             |_____|          <- you
              |   |
             (_) (_)`,
		String.raw`
     HA!   /\_______/\    HA HA!
          /  >     <  \
         |  '       '  |
         |   .-----.   |
         |   \_____/   |
          \           /
           '-._____.-'___________
             |     |   ________ )====-
             |     |  (____)
             |_____|          <- you
              |   |
             (_) (_)`
	];

	let frame = $state(0);

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

	<!-- A fixed width in characters, so swapping frames never moves the page around. -->
	<pre
		class="cat w-[40ch] max-w-full font-mono text-[11px] leading-[1.25] text-ink-2 sm:text-[14px]"
		role="img"
		aria-label={t('common.docsCat')}
		data-testid="docs-cat"
		data-frame={frame}>{frames[frame].replace('<- you', `<- ${t('common.docsYou')}`)}</pre>
</main>

<style>
	/* Shaking with laughter: a little hop and a wobble, from the feet. */
	.cat {
		transform-origin: 40% 95%;
		animation: cackle 0.38s ease-in-out infinite;
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
