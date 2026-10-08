<script lang="ts">
	import { PageCleanup, PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import FilterMenu from './FilterMenu.svelte';
	import { Icon } from '$lib/icons';

	let {
		selectedCount,
		total,
		anyDeleted,
		anyActive = true,
		disabled = false,
		scanning = false,
		onrotate,
		ondelete,
		onrestore,
		onfilter,
		cleanup = PageCleanup.None,
		oncleanup,
		onexpand,
		oncrop,
		onadjust,
		onrescan,
		scoped = false,
		onclear,
		onselectall
	}: {
		selectedCount: number;
		total: number;
		/** At least one page in the frame is deleted, so there is something to restore. */
		anyDeleted: boolean;
		/** At least one page in the frame is not, so there is something to delete. */
		anyActive?: boolean;
		disabled?: boolean;
		/** Crop and rescan need the scanner idle and a single page picked. */
		scanning?: boolean;
		onrotate: (degrees: 90 | -90) => void;
		ondelete: () => void;
		onrestore: () => void;
		onfilter: (filter: PageFilter) => void;
		/** The clean-ups every page in the frame has, which the filter menu shows ticked. */
		cleanup?: PageCleanup;
		oncleanup?: (cleanup: PageCleanup, on: boolean) => void;
		/** Opens the page the edits point at, full screen. Not an edit, but this is where it lives. */
		onexpand?: () => void;
		oncrop?: () => void;
		/** Opens the tone and sharpness dialog for the page in view or the selection. */
		onadjust?: () => void;
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
	 * while a scan runs, and a bar holding nothing but Expand is not an edit bar, so then
	 * there is no bar at all until the feeder stops. The all-pages view keeps its own, for
	 * the selection it shows.
	 */
	const editing = $derived(!disabled);
	const canAim = $derived(editing && !scanning && one);
	const shown = $derived(editing || scoped);

	const tool =
		'flex h-[58px] min-w-[62px] flex-col items-center justify-center gap-1 rounded-[14px] px-2 text-[11px] font-bold transition-colors hover:bg-accent-hover50';
</script>

{#if shown}<div
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
				Rotate left
			</button>
			<button
				type="button"
				class="text-ink {tool}"
				onclick={() => onrotate(90)}
				aria-label="Rotate right"
			>
				<Icon name="rotateRight" />
				Rotate right
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

		{#if onadjust && editing}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onadjust}
				aria-label="Adjust brightness, contrast and sharpness"
				data-testid="adjust-button"
			>
				<Icon name="sliders" />
				Adjust
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

		<!--
		The two take the same place at the end of the row and trade it between them: a page
		that is deleted offers Restore, one that is not offers Delete. Both appear only for a
		selection that is some of each, where both of them mean something.
	-->
		{#if editing && anyDeleted}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onrestore}
				data-testid="restore-button"
			>
				<Icon name="layers" />
				Restore
			</button>
		{/if}
		{#if editing && anyActive}
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
			<FilterMenu {cleanup} {onfilter} {oncleanup} onclose={() => (filterOpen = false)} />
		{/if}
	</div>{/if}
