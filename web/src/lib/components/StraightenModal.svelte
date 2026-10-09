<script lang="ts">
	import { useLayer } from '$lib/keys.svelte';
	import { backdropOut, cardOut } from '$lib/motion';
	import { untrack } from 'svelte';
	import { MAX_SKEW, innerScale, roundSkew } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import Slider from './Slider.svelte';
	import { t } from '$lib/i18n/index.svelte';

	let {
		src,
		alt,
		skew,
		onapply,
		oncancel
	}: {
		/** The page's preview, which already has its current skew applied and cropped. */
		src: string;
		alt: string;
		/** The page's current fine rotation, in degrees. */
		skew: number;
		onapply: (degrees: number) => void;
		oncancel: () => void;
	} = $props();

	const current = untrack(() => skew);
	let draft = $state(current);

	// Page proportions until the image says otherwise; only the ratio matters here.
	let size = $state({ width: 1, height: 1.414 });

	/**
	 * The preview is turned by the change only, then scaled up just enough that the turned
	 * image still fills its frame — what the server does by cropping, so the grid lines up
	 * with what Apply will give back.
	 */
	const delta = $derived(Math.round((draft - current) * 10) / 10);
	const scale = $derived(1 / innerScale(size.width, size.height, delta));

	function nudge(by: number) {
		draft = roundSkew(draft + by);
	}

	let dialog = $state<HTMLDivElement | null>(null);
	$effect(() => {
		dialog?.focus();
	});

	// Escape cancels, through the shared layer stack: only this closes, not the preview under it.
	useLayer(() => oncancel());

	const nudgeBtn =
		'flex h-11 w-11 flex-none items-center justify-center rounded-full bg-ctl text-ink transition-colors hover:bg-ctl-hover disabled:pointer-events-none disabled:opacity-40';
</script>

<div
	class="fixed inset-0 z-50 flex animate-fade-in items-center justify-center bg-[rgb(19_26_43_/_0.38)] p-4 backdrop-blur-[3px]"
	out:backdropOut|global
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label={t('dialogs.cancelStraighten')}
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full w-[min(640px,100%)] animate-card-in flex-col gap-[18px] overflow-y-auto rounded-[26px] bg-surface p-6 text-ink shadow-modal outline-none"
		out:cardOut|global
		role="dialog"
		aria-modal="true"
		aria-label={t('dialogs.straightenTitle')}
		tabindex="-1"
		bind:this={dialog}
		data-testid="straighten-modal"
	>
		<h2 class="text-[20px] font-extrabold">{t('dialogs.straightenTitle')}</h2>

		<div class="flex flex-wrap items-start gap-[18px]">
			<div
				class="relative mx-auto w-full max-w-[250px] flex-none overflow-hidden rounded-[4px] shadow-page"
			>
				<img
					{src}
					{alt}
					class="block w-full transition-transform duration-100"
					style:transform="rotate({delta}deg) scale({scale})"
					onload={(e) => {
						const img = e.currentTarget as HTMLImageElement;
						size = { width: img.naturalWidth, height: img.naturalHeight };
					}}
					data-testid="straighten-preview"
				/>
				<!-- A fixed grid to line the text up against while the page turns under it. -->
				<div
					class="pointer-events-none absolute inset-0"
					style="background-image: linear-gradient(to right, rgb(43 99 240 / 0.35) 1px, transparent 1px), linear-gradient(to bottom, rgb(43 99 240 / 0.35) 1px, transparent 1px); background-size: 10% 10%"
					aria-hidden="true"
				></div>
			</div>

			<div class="flex min-w-[220px] flex-1 flex-col gap-3">
				<Slider
					name="straighten"
					label={t('dialogs.angle')}
					min={-MAX_SKEW}
					max={MAX_SKEW}
					step={0.1}
					value={draft}
					onchange={(v) => (draft = roundSkew(v))}
				/>
				<div class="flex items-center gap-2">
					<button
						type="button"
						class={nudgeBtn}
						onclick={() => nudge(-0.1)}
						disabled={draft <= -MAX_SKEW}
						aria-label={t('dialogs.turnAnticlockwise')}
						data-testid="straighten-left"
					>
						<Icon name="rotateLeft" />
					</button>
					<span class="flex-1 text-center text-[12px] text-ink-muted">
						{t('dialogs.lineUp')}
					</span>
					<button
						type="button"
						class={nudgeBtn}
						onclick={() => nudge(0.1)}
						disabled={draft >= MAX_SKEW}
						aria-label={t('dialogs.turnClockwise')}
						data-testid="straighten-right"
					>
						<Icon name="rotateRight" />
					</button>
				</div>
			</div>
		</div>

		<div class="flex flex-wrap items-center justify-end gap-2">
			<button
				type="button"
				class="h-[46px] rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover"
				onclick={oncancel}
			>
				{t('dialogs.cancel')}
			</button>
			<button
				type="button"
				class="flex h-[46px] items-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 px-5 text-[15px] font-extrabold text-white shadow-export transition-shadow disabled:pointer-events-none disabled:opacity-40"
				disabled={draft === current}
				onclick={() => onapply(draft)}
				data-testid="straighten-apply"
			>
				{t('dialogs.apply')}
			</button>
		</div>
	</div>
</div>
