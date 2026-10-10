<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Icon, type IconName } from '$lib/icons';

	let {
		title,
		hint = '',
		icon,
		testid,
		children
	}: {
		title: string;
		hint?: string;
		icon?: IconName;
		testid?: string;
		children: Snippet;
	} = $props();
</script>

<section
	class="col-span-full grid gap-x-10 gap-y-3.5 border-t border-line pt-5 first:border-t-0 first:pt-0 md:grid-cols-[200px_minmax(0,1fr)] md:pt-7"
	data-testid={testid}
>
	<div class="flex items-start gap-3 md:flex-col md:gap-2.5">
		{#if icon}
			<span
				class="flex h-9 w-9 flex-none items-center justify-center rounded-[11px] bg-accent-tint text-accent"
				aria-hidden="true"
			>
				<Icon name={icon} />
			</span>
		{/if}
		<div class="flex min-w-0 flex-col gap-0.5">
			<h2 class="text-[15px] font-extrabold tracking-[-0.01em]">{title}</h2>
			{#if hint}
				<p class="text-[12px] leading-snug text-ink-muted">{hint}</p>
			{/if}
		</div>
	</div>

	<div
		class="grid min-w-0 items-start gap-x-6 gap-y-5 sm:grid-cols-[repeat(auto-fill,minmax(280px,1fr))]"
	>
		{@render children()}
	</div>
</section>
