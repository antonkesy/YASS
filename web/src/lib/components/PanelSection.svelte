<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Icon } from '$lib/icons';

	let {
		title,
		summary = '',
		open = false,
		testid = '',
		summaryTestid = '',
		ontoggle,
		children
	}: {
		title: string;
		/**
		 * What the section says while it is shut. A summary is the whole point of shutting one:
		 * the heading alone tells you nothing you could not have guessed.
		 */
		summary?: string;
		open?: boolean;
		testid?: string;
		summaryTestid?: string;
		ontoggle?: (open: boolean) => void;
		children: Snippet;
	} = $props();
</script>

<!--
	A real <details>, not a div and a boolean: the keyboard handling, the announcement as a
	disclosure and the shut-by-default all come with the element, and there is no second copy
	of "is it open" to keep in step with the first. Nothing interactive belongs in <summary> —
	it is itself the control, and a button inside it cannot be reached.
-->
<details
	class="group rounded-[16px] bg-app px-3"
	{open}
	ontoggle={(e) => ontoggle?.(e.currentTarget.open)}
	data-testid={testid || undefined}
>
	<!--
		-mx-3/px-3 so the hover wash reaches the card's own edges: without something changing
		under the pointer the only hint this row is a control is the cursor.
	-->
	<summary
		class="-mx-3 flex cursor-pointer list-none items-center gap-2 rounded-[16px] px-3 py-2.5 transition-colors select-none hover:bg-ctl [&::-webkit-details-marker]:hidden"
	>
		<span class="flex-none text-xs font-bold tracking-[0.06em] text-ink-muted uppercase">
			{title}
		</span>
		<span
			class="ml-auto min-w-0 truncate text-[13px] font-semibold text-ink-muted"
			data-testid={summaryTestid || undefined}
		>
			{summary}
		</span>
		<Icon
			name="chevronRight"
			size={14}
			class="flex-none text-ink-faint transition-transform duration-200 group-open:rotate-90"
		/>
	</summary>
	<div class="flex flex-col gap-1 pb-3">
		{@render children()}
	</div>
</details>
