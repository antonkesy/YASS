<script lang="ts">
	import { PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import { pageFilters } from '$lib/review/edits';
	import { Icon } from '$lib/icons';

	let {
		selectedCount,
		total,
		anyDeleted,
		disabled = false,
		scanning = false,
		onrotate,
		ondelete,
		onrestore,
		onfilter,
		onexpand,
		oncrop,
		onrescan,
		onsplit,
		splits = false,
		firstPage = false,
		onclear
	}: {
		selectedCount: number;
		total: number;
		anyDeleted: boolean;
		disabled?: boolean;
		/** Crop and rescan need the scanner idle and a single page picked. */
		scanning?: boolean;
		onrotate: (degrees: 90 | -90) => void;
		ondelete: () => void;
		onrestore: () => void;
		onfilter: (filter: PageFilter) => void;
		/** Opens the page the edits point at, full screen. Not an edit, but this is where it lives. */
		onexpand?: () => void;
		oncrop?: () => void;
		onrescan?: () => void;
		/** Toggles whether the target page opens a document. */
		onsplit?: (split: boolean) => void;
		/** Whether the target page currently opens a document. */
		splits?: boolean;
		/** The target page is the first one, which always opens a document. */
		firstPage?: boolean;
		onclear: () => void;
	} = $props();

	let filterOpen = $state(false);

	const scope = $derived(selectedCount === 0 ? `all ${total}` : `${selectedCount} selected`);
	const one = $derived(selectedCount === 1);

	const tool =
		'flex h-[58px] min-w-[62px] flex-col items-center justify-center gap-1 rounded-[14px] px-2 text-[11px] font-bold transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-35';
</script>

<div
	class="relative flex flex-wrap items-center gap-0.5 rounded-[20px] bg-surface p-1.5 shadow-toolbar"
	role="toolbar"
	aria-label="Page edits"
	data-testid="toolbar"
>
	<span class="px-3 text-[13px] font-semibold text-ink-muted" data-testid="toolbar-scope">
		Acting on {scope}
	</span>

	{#if selectedCount > 0}
		<button
			type="button"
			class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
			onclick={onclear}
		>
			Clear
		</button>
	{/if}

	<span class="mx-1 h-8 w-px bg-line" aria-hidden="true"></span>

	{#if onexpand}
		<!-- Looking at a page is not editing it, so this one stays on while a scan runs. -->
		<button
			type="button"
			class="text-ink {tool}"
			disabled={total === 0}
			onclick={onexpand}
			aria-label="Open this page full screen"
			data-testid="expand-button"
		>
			<Icon name="expand" />
			Expand
		</button>
	{/if}

	<button
		type="button"
		class="text-ink {tool}"
		{disabled}
		onclick={() => onrotate(-90)}
		aria-label="Rotate left"
	>
		<Icon name="rotateLeft" />
		Left
	</button>
	<button
		type="button"
		class="text-ink {tool}"
		{disabled}
		onclick={() => onrotate(90)}
		aria-label="Rotate right"
	>
		<Icon name="rotateRight" />
		Right
	</button>

	{#if oncrop}
		<button
			type="button"
			class="text-ink {tool}"
			disabled={disabled || scanning || !one}
			onclick={oncrop}
			aria-label="Crop page"
			data-testid="crop-button"
		>
			<Icon name="crop" />
			Crop
		</button>
	{/if}

	<button
		type="button"
		class="{filterOpen ? 'bg-accent-50 text-accent' : 'text-ink'} {tool}"
		{disabled}
		onclick={() => (filterOpen = !filterOpen)}
		aria-expanded={filterOpen}
		aria-haspopup="menu"
		aria-label="Filter"
		data-testid="filter-button"
	>
		<Icon name="wand" />
		Filter
	</button>

	{#if onrescan}
		<button
			type="button"
			class="text-ink {tool}"
			disabled={disabled || scanning || !one}
			onclick={onrescan}
			aria-label="Rescan this page"
			data-testid="rescan-button"
		>
			<Icon name="rescan" />
			Rescan
		</button>
	{/if}

	{#if onsplit}
		<button
			type="button"
			class="{splits ? 'bg-accent-50 text-accent' : 'text-ink'} {tool}"
			disabled={disabled || firstPage}
			onclick={() => onsplit(!splits)}
			aria-pressed={splits}
			aria-label={splits ? 'Join with previous page' : 'Split before this page'}
			data-testid="split-button"
		>
			<Icon name="scissors" />
			{splits ? 'Join' : 'Split'}
		</button>
	{/if}

	<button
		type="button"
		class="text-ink {tool}"
		disabled={disabled || !anyDeleted}
		onclick={onrestore}
		data-testid="restore-button"
	>
		<Icon name="layers" />
		Restore
	</button>
	<button
		type="button"
		class="text-danger hover:bg-danger-bg! {tool}"
		{disabled}
		onclick={ondelete}
		data-testid="delete-button"
	>
		<Icon name="trash" />
		Delete
	</button>

	{#if filterOpen}
		<button
			type="button"
			class="fixed inset-0 z-30 cursor-default"
			aria-label="Close filter menu"
			onclick={() => (filterOpen = false)}
		></button>
		<div
			class="absolute bottom-[calc(100%+8px)] left-1/2 z-31 min-w-[200px] -translate-x-1/2 animate-fade-in rounded-[16px] bg-surface p-1.5 shadow-menu"
			role="menu"
			data-testid="filter-menu"
		>
			{#each pageFilters as option (option.value)}
				<button
					type="button"
					role="menuitem"
					class="flex h-[42px] w-full items-center gap-2.5 rounded-[10px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50"
					onclick={() => {
						onfilter(option.value);
						filterOpen = false;
					}}
				>
					<span
						class="h-7 w-[22px] flex-none rounded-[5px] shadow-[0_0_0_1px_rgb(19_26_43_/_0.1)]"
						style="background: linear-gradient(135deg, {option.swatch})"
						aria-hidden="true"
					></span>
					{option.label}
				</button>
			{/each}
		</div>
	{/if}
</div>
