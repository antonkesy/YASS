<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		tone,
		title,
		hint = null,
		detail = null
	}: {
		/** `warning` for something to sort out (no scanner, no connection); `danger` for a scanner fault. */
		tone: 'warning' | 'danger';
		title: string;
		/** What to do about it. */
		hint?: string | null;
		/** What the scanner or driver said, in its own words. */
		detail?: string | null;
	} = $props();
</script>

<!--
	The scanner's trouble, as a card that pops in over the stage rather than a line of grey
	text under it: it is the one thing on the screen that needs doing before a scan can.
-->
<div
	class="flex w-full max-w-[440px] animate-pop-in items-start gap-3 rounded-[18px] px-4 py-3.5 text-left shadow-chip {tone ===
	'danger'
		? 'bg-danger-bg'
		: 'bg-warning-bg'}"
	role="alert"
	data-testid="scanner-trouble"
	data-tone={tone}
>
	<span
		class="flex h-9 w-9 flex-none items-center justify-center rounded-full bg-surface {tone ===
		'danger'
			? 'text-danger'
			: 'text-warning-ink'}"
	>
		<Icon name="alert" />
	</span>
	<span class="flex min-w-0 flex-col gap-0.5">
		<span
			class="text-[15px] font-extrabold {tone === 'danger' ? 'text-danger' : 'text-warning-ink'}"
			>{title}</span
		>
		{#if hint}
			<span class="text-[13px] leading-snug text-ink-2">{hint}</span>
		{/if}
		{#if detail}
			<span class="text-[12px] leading-snug break-words text-ink-muted" data-testid="scanner-detail"
				>{detail}</span
			>
		{/if}
	</span>
</div>
