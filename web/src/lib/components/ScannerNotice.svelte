<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Icon } from '$lib/icons';

	let {
		tone,
		title,
		hint = null,
		detail = null,
		children
	}: {
		tone: 'warning' | 'danger';
		title: string;
		hint?: string | null;
		detail?: string | null;
		/** An action under the text. */
		children?: Snippet;
	} = $props();
</script>

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
			<span
				class="text-[12px] leading-snug break-words text-ink-muted select-text"
				data-testid="scanner-detail">{detail}</span
			>
		{/if}
		{#if children}
			<span class="-ml-3.5 flex pt-1">{@render children()}</span>
		{/if}
	</span>
</div>
