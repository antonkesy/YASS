<script lang="ts">
	import type { Snippet } from 'svelte';

	let {
		title,
		hint = '',
		testid,
		children
	}: {
		title: string;
		/** One line on what the section is for, where the heading alone leaves it open. */
		hint?: string;
		testid?: string;
		children: Snippet;
	} = $props();
</script>

<!--
	A heading and a rule, so a sheet with four unrelated groups of controls in it reads as
	four groups rather than one long column. Full width of the sheet's grid; what goes
	inside brings its own columns.
-->
<section
	class="col-span-full flex flex-col gap-3 border-t border-line pt-4 first:border-t-0 first:pt-0"
	data-testid={testid}
>
	<div class="flex flex-col gap-0.5">
		<h2 class="text-[15px] font-extrabold tracking-[-0.01em]">{title}</h2>
		{#if hint}
			<p class="text-[12px] text-ink-muted">{hint}</p>
		{/if}
	</div>

	<!--
		The same auto-fit grid the scanner form uses, so a wide sheet puts two controls side by
		side instead of one stretched across it. Anything that needs the width says so with
		`col-span-full`.
	-->
	<div
		class="grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(340px,1fr))]"
	>
		{@render children()}
	</div>
</section>
