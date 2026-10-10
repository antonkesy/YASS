<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { t } from '$lib/i18n/index.svelte';
	import { PageCleanup, PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import FilterMenu from './FilterMenu.svelte';
	import Popover from './Popover.svelte';
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
		onstraighten,
		onautocrop,
		autocropping = false,
		onadjust,
		onrescan,
		scoped = false,
		onclear,
		onselectall
	}: {
		selectedCount: number;
		total: number;
		anyDeleted: boolean;
		anyActive?: boolean;
		disabled?: boolean;
		scanning?: boolean;
		onrotate: (degrees: 90 | -90) => void;
		ondelete: () => void;
		onrestore: () => void;
		onfilter: (filter: PageFilter) => void;
		cleanup?: PageCleanup;
		oncleanup?: (cleanup: PageCleanup, on: boolean) => void;
		onexpand?: () => void;
		oncrop?: () => void;
		onstraighten?: () => void;
		onautocrop?: () => void;
		autocropping?: boolean;
		onadjust?: () => void;
		onrescan?: () => void;
		scoped?: boolean;
		onclear: () => void;
		onselectall?: () => void;
	} = $props();

	let filterOpen = $state(false);
	let cropOpen = $state(false);

	const scope = $derived(
		selectedCount === 0
			? t('review.actingOnPage')
			: t('review.actingOnSelected', { n: selectedCount })
	);

	const one = $derived(selectedCount <= 1);

	const editing = $derived(!disabled);
	const canAim = $derived(editing && !scanning && one);
	const shown = $derived(editing || scoped);

	// Icons only: each button's aria-label doubles as its tooltip.
	const tool =
		'flex h-11 w-11 flex-none items-center justify-center rounded-[14px] transition-colors hover:bg-accent-hover50';
</script>

{#if shown}<div
		class="relative flex flex-wrap items-center gap-0.5 rounded-[20px] bg-surface p-1.5 shadow-toolbar"
		role="toolbar"
		aria-label={t('review.pageEdits')}
		{@attach autoAnimate}
		data-testid="toolbar"
	>
		{#if scoped}
			<span class="px-3 text-[13px] font-semibold text-ink-muted" data-testid="toolbar-scope">
				{scope}
			</span>

			{#if selectedCount > 0}
				<button
					type="button"
					class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
					onclick={onclear}
				>
					{t('review.clear')}
				</button>
			{:else if onselectall && total > 1 && editing}
				<button
					type="button"
					class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
					onclick={onselectall}
					data-testid="toolbar-select-all"
				>
					{t('review.allN', { n: total })}
				</button>
			{/if}

			<span class="mx-1 h-8 w-px bg-line" aria-hidden="true"></span>
		{/if}

		{#if onexpand && total > 0}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onexpand}
				aria-label={t('review.openFullScreen')}
				title={t('review.openFullScreen')}
				data-testid="expand-button"
			>
				<Icon name="expand" />
			</button>
		{/if}

		{#if editing}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={() => onrotate(-90)}
				aria-label={t('review.rotateLeft')}
				title={t('review.rotateLeft')}
			>
				<Icon name="rotateLeft" />
			</button>
			<button
				type="button"
				class="text-ink {tool}"
				onclick={() => onrotate(90)}
				aria-label={t('review.rotateRight')}
				title={t('review.rotateRight')}
			>
				<Icon name="rotateRight" />
			</button>
		{/if}

		{#if editing && !scanning && ((one && (oncrop || onstraighten)) || onautocrop)}
			<button
				type="button"
				class="{cropOpen ? 'bg-accent-50 text-accent' : 'text-ink'} {tool}"
				onclick={() => (cropOpen = !cropOpen)}
				aria-expanded={cropOpen}
				aria-haspopup="menu"
				aria-label={t('review.cropAndStraighten')}
				title={t('review.cropAndStraighten')}
				data-testid="crop-button"
			>
				<Icon name="crop" />
			</button>
		{/if}

		{#if onadjust && editing}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onadjust}
				aria-label={t('review.adjustLong')}
				title={t('review.adjustLong')}
				data-testid="adjust-button"
			>
				<Icon name="sliders" />
			</button>
		{/if}

		{#if editing}
			<button
				type="button"
				class="{filterOpen ? 'bg-accent-50 text-accent' : 'text-ink'} {tool}"
				onclick={() => (filterOpen = !filterOpen)}
				aria-expanded={filterOpen}
				aria-haspopup="menu"
				aria-label={t('review.filter')}
				title={t('review.filter')}
				data-testid="filter-button"
			>
				<Icon name="wand" />
			</button>
		{/if}

		{#if onrescan && canAim}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onrescan}
				aria-label={t('review.rescanPage')}
				title={t('review.rescanPage')}
				data-testid="rescan-button"
			>
				<Icon name="rescan" />
			</button>
		{/if}

		{#if editing && anyDeleted}
			<button
				type="button"
				class="text-ink {tool}"
				onclick={onrestore}
				aria-label={t('review.restore')}
				title={t('review.restore')}
				data-testid="restore-button"
			>
				<Icon name="layers" />
			</button>
		{/if}
		{#if editing && anyActive}
			<button
				type="button"
				class="text-danger hover:bg-danger-bg! {tool}"
				onclick={ondelete}
				aria-label={t('review.delete')}
				title={t('review.delete')}
				data-testid="delete-button"
			>
				<Icon name="trash" />
			</button>
		{/if}

		{#if cropOpen}
			<Popover
				label={t('review.closeCropMenu')}
				onclose={() => (cropOpen = false)}
				class="absolute bottom-[calc(100%+8px)] left-1/2 z-31 min-w-[220px] -translate-x-1/2 animate-fade-in rounded-[16px] bg-surface p-1.5 shadow-menu"
				testid="crop-menu"
			>
				{#each [{ id: 'crop', label: t('review.cropMenuItem'), icon: 'crop', run: oncrop }, { id: 'straighten', label: t('review.straightenMenuItem'), icon: 'rotateRight', run: onstraighten }] as const as item (item.id)}
					{#if item.run && one}
						{@const run = item.run}
						<button
							type="button"
							role="menuitem"
							class="flex h-[44px] w-full items-center gap-2.5 rounded-[10px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50"
							onclick={() => {
								cropOpen = false;
								run();
							}}
							data-testid="{item.id}-open"
						>
							<Icon name={item.icon} />
							{item.label}
						</button>
					{/if}
				{/each}
				{#if onautocrop}
					<button
						type="button"
						role="menuitem"
						class="flex min-h-[44px] w-full items-center gap-2.5 rounded-[10px] px-2.5 py-1 text-left transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-50"
						disabled={autocropping}
						onclick={() => {
							cropOpen = false;
							onautocrop();
						}}
						data-testid="autocrop"
					>
						<Icon name="scissors" />
						<span class="flex flex-col">
							<span class="text-sm font-semibold">
								{autocropping ? t('review.findingBorders') : t('review.autoCrop')}
							</span>
							<span class="text-[11px] text-ink-muted">
								{selectedCount > 1
									? t('review.trimPages', { n: selectedCount })
									: t('review.trimThisPage')}
							</span>
						</span>
					</button>
				{/if}
			</Popover>
		{/if}

		{#if filterOpen}
			<FilterMenu {cleanup} {onfilter} {oncleanup} onclose={() => (filterOpen = false)} />
		{/if}
	</div>{/if}
