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
		scoped = false,
		onclear,
		onselectall
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
		/**
		 * Whether this toolbar manages a selection. Only the all-pages view does: the review
		 * screen shows one page at a time, and a toolbar under it that could be acting on
		 * some other set of pages is a question nobody wanted to have to ask.
		 */
		scoped?: boolean;
		onclear: () => void;
		/** Takes the whole scan, which is the way out of acting on one page at a time. */
		onselectall?: () => void;
	} = $props();

	let filterOpen = $state(false);

	/**
	 * Nothing ticked means the page on the stage, the one these buttons sit under. Acting on
	 * the whole scan by default was a rotation of everything one button away from a rotation
	 * of the page someone was looking at, with nothing on screen to tell them apart.
	 */
	const scope = $derived(selectedCount === 0 ? 'this page' : `${selectedCount} selected`);

	/**
	 * Whether the edits point at exactly one page, which is what Crop and Rescan need. Nothing
	 * ticked counts: that is the page on the stage, and it is the one they act on.
	 */
	const one = $derived(selectedCount <= 1);

	/**
	 * A button is here when pressing it would do something, and absent when it would not —
	 * no shelf of grey buttons explaining what you cannot have. Editing stops altogether
	 * while a scan runs, so the row empties down to Expand until the feeder does.
	 */
	const editing = $derived(!disabled);
	const canAim = $derived(editing && !scanning && one);

	const tool =
		'flex h-[58px] min-w-[62px] flex-col items-center justify-center gap-1 rounded-[14px] px-2 text-[11px] font-bold transition-colors hover:bg-accent-hover50';
</script>

<div
	class="relative flex flex-wrap items-center gap-0.5 rounded-[20px] bg-surface p-1.5 shadow-toolbar"
	role="toolbar"
	aria-label="Page edits"
	data-testid="toolbar"
>
	{#if scoped}
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
		{:else if onselectall && total > 1 && editing}
			<button
				type="button"
				class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
				onclick={onselectall}
				data-testid="toolbar-select-all"
			>
				All {total}
			</button>
		{/if}

		<span class="mx-1 h-8 w-px bg-line" aria-hidden="true"></span>
	{/if}

	{#if onexpand && total > 0}
		<!-- Looking at a page is not editing it, so this one stays on while a scan runs. -->
		<button
			type="button"
			class="text-ink {tool}"
			onclick={onexpand}
			aria-label="Open this page full screen"
			data-testid="expand-button"
		>
			<Icon name="expand" />
			Expand
		</button>
	{/if}

	{#if editing}
		<button
			type="button"
			class="text-ink {tool}"
			onclick={() => onrotate(-90)}
			aria-label="Rotate left"
		>
			<Icon name="rotateLeft" />
			Left
		</button>
		<button
			type="button"
			class="text-ink {tool}"
			onclick={() => onrotate(90)}
			aria-label="Rotate right"
		>
			<Icon name="rotateRight" />
			Right
		</button>
	{/if}

	{#if oncrop && canAim}
		<button
			type="button"
			class="text-ink {tool}"
			onclick={oncrop}
			aria-label="Crop page"
			data-testid="crop-button"
		>
			<Icon name="crop" />
			Crop
		</button>
	{/if}

	{#if editing}
		<button
			type="button"
			class="{filterOpen ? 'bg-accent-50 text-accent' : 'text-ink'} {tool}"
			onclick={() => (filterOpen = !filterOpen)}
			aria-expanded={filterOpen}
			aria-haspopup="menu"
			aria-label="Filter"
			data-testid="filter-button"
		>
			<Icon name="wand" />
			Filter
		</button>
	{/if}

	{#if onrescan && canAim}
		<button
			type="button"
			class="text-ink {tool}"
			onclick={onrescan}
			aria-label="Rescan this page"
			data-testid="rescan-button"
		>
			<Icon name="rescan" />
			Rescan
		</button>
	{/if}

	{#if editing && anyDeleted}
		<button type="button" class="text-ink {tool}" onclick={onrestore} data-testid="restore-button">
			<Icon name="layers" />
			Restore
		</button>
	{/if}
	{#if editing}
		<button
			type="button"
			class="text-danger hover:bg-danger-bg! {tool}"
			onclick={ondelete}
			data-testid="delete-button"
		>
			<Icon name="trash" />
			Delete
		</button>
	{/if}

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
