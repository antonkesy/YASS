<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { slide } from 'svelte/transition';
	import { ms } from '$lib/motion';
	import { formatTime } from '$lib/format';
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';
	import type { ScannerRow } from '$lib/scanners';

	let {
		rows,
		chosen = '',
		onrename,
		onhide,
		onforget
	}: {
		rows: ScannerRow[];
		chosen?: string;
		onrename: (id: string, label: string) => void;
		onhide: (id: string, hidden: boolean) => void;
		onforget: (id: string) => void;
	} = $props();

	let editing = $state<string | null>(null);
	let draft = $state('');
	let open = $state<string | null>(null);

	function startRename(row: ScannerRow) {
		editing = row.id;
		draft = row.name;
	}

	function commit(id: string) {
		if (editing !== id) return;
		onrename(id, draft);
		editing = null;
	}

	function facts(row: ScannerRow): [string, string][] {
		return (
			[
				[t('settings.factDriverName'), row.labelled ? row.original : null],
				[t('settings.factDriver'), row.driver ?? null],
				[t('settings.factAddress'), row.connection ?? null],
				[t('settings.factDeviceId'), row.id],
				[t('settings.factFirstSeen'), row.firstSeen ? formatTime(row.firstSeen) : null],
				[
					t('settings.factLastSeen'),
					row.connected ? t('settings.factNow') : row.lastSeen ? formatTime(row.lastSeen) : null
				],
				[t('settings.factScans'), row.scans > 0 ? String(row.scans) : null],
				[t('settings.factLastScan'), row.lastUsed ? formatTime(row.lastUsed) : null]
			] as [string, string | null][]
		).filter((f): f is [string, string] => f[1] !== null && f[1] !== '');
	}

	const action =
		'flex h-9 flex-none items-center gap-1.5 rounded-full px-3 text-xs font-bold transition-colors';
</script>

{#if rows.length === 0}
	<p class="col-span-full rounded-[16px] bg-app px-4 py-3 text-[13px] text-ink-muted">
		{t('settings.noScanners')}
	</p>
{:else}
	<ul class="col-span-full flex flex-col gap-2" data-testid="scanner-list" {@attach autoAnimate}>
		{#each rows as row (row.id)}
			{@const expanded = open === row.id}
			<li
				class="rounded-[16px] px-3.5 py-2.5 transition-colors {row.connected
					? 'bg-app'
					: 'border-[1.5px] border-dashed border-line-strong bg-surface'}"
				data-testid="scanner-row"
				data-connected={row.connected}
				data-hidden={row.hidden}
			>
				<div class="flex flex-wrap items-center gap-x-3 gap-y-2">
					<span
						class="h-2.5 w-2.5 flex-none rounded-full {row.connected
							? 'bg-success'
							: 'shadow-[inset_0_0_0_1.5px] shadow-ink-faint'}"
						aria-hidden="true"
					></span>

					<div class="flex min-w-0 flex-1 flex-col {row.connected ? '' : 'opacity-60'}">
						{#if editing === row.id}
							<input
								class="field h-9 px-2.5 text-sm font-bold"
								aria-label={t('settings.nameFor', { name: row.original })}
								bind:value={draft}
								onkeydown={(e) => {
									if (e.key === 'Enter') commit(row.id);
									else if (e.key === 'Escape') {
										e.stopPropagation();
										editing = null;
									}
								}}
								onblur={() => commit(row.id)}
								{@attach (el) => el.select()}
								data-testid="scanner-name-input"
							/>
						{:else}
							<span class="flex min-w-0 items-center gap-2">
								<span class="truncate text-sm font-bold" data-testid="scanner-name">{row.name}</span
								>
								{#if row.hidden}
									<span
										class="flex-none rounded-full bg-ctl px-2 py-0.5 text-[11px] font-semibold text-ink-muted"
										>{t('settings.hidden')}</span
									>
								{/if}
							</span>
						{/if}
						<span class="truncate text-xs text-ink-muted">
							{row.connected ? t('settings.connected') : t('settings.notConnected')}{row.driver
								? ` · ${row.driver}`
								: ''}{!row.connected && row.lastSeen
								? ` · ${t('settings.lastSeenAt', { time: formatTime(row.lastSeen) })}`
								: ''}{row.labelled ? ` · ${row.original}` : ''}
						</span>
					</div>

					<div class="flex flex-none items-center gap-0.5">
						<button
							type="button"
							class="{action} text-accent hover:bg-accent-wash"
							onclick={() => startRename(row)}
							aria-label={t('settings.renameLabel', { name: row.name })}
							data-testid="scanner-rename"
						>
							<Icon name="pencil" />
							<span class="hidden sm:inline">{t('settings.rename')}</span>
						</button>
						<button
							type="button"
							class="{action} text-ink-3 hover:bg-ctl disabled:pointer-events-none disabled:opacity-40"
							onclick={() => onhide(row.id, !row.hidden)}
							disabled={!row.hidden && row.id === chosen}
							title={!row.hidden && row.id === chosen
								? t('settings.chosenTitle')
								: row.hidden
									? t('settings.showTitle')
									: t('settings.hideTitle')}
							aria-pressed={row.hidden}
							data-testid="scanner-hide"
						>
							{row.hidden ? t('settings.show') : t('settings.hide')}
						</button>
						<button
							type="button"
							class="flex h-9 w-9 flex-none items-center justify-center rounded-full text-ink-3 transition-[background-color,transform] hover:bg-ctl {expanded
								? 'rotate-90'
								: ''}"
							aria-expanded={expanded}
							aria-label={t('settings.detailsFor', { name: row.name })}
							onclick={() => (open = expanded ? null : row.id)}
							data-testid="scanner-details-toggle"
						>
							<Icon name="chevronRight" />
						</button>
					</div>
				</div>

				{#if expanded}
					<div
						class="mt-2.5 border-t border-line-strong pt-2.5"
						transition:slide={{ duration: ms(220) }}
					>
						<dl
							class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-[12px]"
							data-testid="scanner-facts"
						>
							{#each facts(row) as [key, value] (key)}
								<dt class="font-semibold text-ink-muted">{key}</dt>
								<dd class="min-w-0 font-mono break-all text-ink-2">{value}</dd>
							{/each}
						</dl>
						{#if !row.connected && row.scans === 0}
							<!-- Past scans still name one that was used, so only an unused ghost can go. -->
							<button
								type="button"
								class="{action} mt-2 -ml-3 text-danger hover:bg-danger-bg"
								onclick={() => onforget(row.id)}
								data-testid="scanner-forget"
							>
								<Icon name="trash" />
								{t('settings.forget')}
							</button>
						{/if}
					</div>
				{/if}
			</li>
		{/each}
	</ul>
{/if}
