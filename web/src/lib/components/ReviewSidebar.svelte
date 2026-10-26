<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { canShare } from '$lib/hub/bridge';
	import { getDesktop, getHub } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { ExportFormat, type SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import type { MessageKey } from '$lib/i18n/index.svelte';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import {
		activePageCount,
		colorLabel,
		documentName,
		exportFormatOptions,
		formatBytes,
		formatTime,
		pageSizeLabel,
		sourceLabel
	} from '$lib/format';
	import { exportPrefs, nameTemplate, scannerPrefs } from '$lib/presets';
	import { exportBaseName, fileExtensions, savePicker } from '$lib/review/download';
	import type { Review } from '$lib/review/review.svelte';
	import { scannerName } from '$lib/scanners';
	import ExportBar from './ExportBar.svelte';
	import NameField from './NameField.svelte';
	import PdfProperties from './PdfProperties.svelte';
	import Segmented from './Segmented.svelte';

	let {
		session,
		review,
		mergeable,
		onmerge
	}: {
		session: SessionDto;
		review: Review;
		mergeable: number;
		onmerge: () => void;
	} = $props();

	const client = getHub();
	const desktop = getDesktop();
	const exportProgress = client.exportProgress;
	const serverFolder = client.serverFolder;
	const scannerStatus = client.scanner;

	const editable = $derived(
		session.State === SessionState.Review || session.State === SessionState.Done
	);
	const busyReason = $derived(
		session.State === SessionState.Scanning
			? t('review.waitScan')
			: session.State === SessionState.Exporting
				? t('review.waitSave')
				: t('review.notEditable')
	);

	// Same fallback as the past scans list: an unnamed scan reads as the name template.
	const displayName = $derived(
		documentName({ ...session, Name: review.storedName ?? undefined }, nameTemplate($exportPrefs))
	);

	const scannerLabel = $derived(
		session.OptionsUsed.Device
			? scannerName($scannerPrefs, session.OptionsUsed.Device)
			: ($scannerStatus?.DeviceName ?? '')
	);
	const scanSettings = $derived.by((): [MessageKey, string][] => {
		const o = session.OptionsUsed;
		const all: [MessageKey, string][] = [
			['settings.source', sourceLabel(o.Source)],
			['settings.resolution', o.Dpi ? t('home.dpi', { n: o.Dpi }) : ''],
			['settings.colourMode', colorLabel(o.Color)],
			['settings.paperSize', pageSizeLabel(o.PageSize)]
		];
		return all.filter(([, value]) => value);
	});
	const pageSummary = $derived(
		t('review.pageSummary', { active: activePageCount(session), n: session.Pages.length })
	);

	let confirmDiscard = $state(false);

	const ghost =
		'flex h-[42px] items-center gap-2.5 rounded-[12px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-40';
</script>

{#snippet heading(title: string, value = '', testid = '')}
	<div class="flex items-baseline gap-2">
		<span class="text-[13px] font-semibold text-ink-muted">{title}</span>
		{#if value}
			<span
				class="ml-auto min-w-0 truncate text-[13px] font-semibold text-ink-muted"
				data-testid={testid || undefined}>{value}</span
			>
		{/if}
	</div>
{/snippet}

{#snippet tile(label: string, value: string, wide = false)}
	<div
		class="flex min-w-0 flex-col gap-0.5 rounded-[12px] bg-app px-2.5 py-2 {wide
			? 'col-span-2'
			: ''}"
	>
		<span class="truncate text-[11px] text-ink-muted">{label}</span>
		<span class="truncate text-[13px] font-bold tracking-[-0.01em] tabular-nums" title={value}
			>{value}</span
		>
	</div>
{/snippet}

<!-- Width kept near the filmstrip's so the stage between them stays roughly centred. -->
<aside
	class="order-3 flex w-full min-w-0 basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:min-h-0 lg:w-auto lg:basis-(--review-aside-w) lg:overflow-y-auto"
	{@attach autoAnimate}
>
	{@render heading(t('review.headingName'))}

	<NameField
		value={displayName}
		placeholder={$exportPrefs.useNameTemplate
			? exportBaseName(session, $exportPrefs.nameTemplate)
			: t('review.documentName')}
		extension={fileExtensions[$exportPrefs.format]}
		onrename={(name) => review.rename(name)}
	/>

	{@render heading(t('review.headingSave'))}

	<ExportBar
		{session}
		progress={$exportProgress[session.Id] ?? null}
		busy={review.exporting}
		picker={desktop !== null || savePicker() !== null}
		ondownload={() => review.saveAs()}
		serverFolder={$exportPrefs.serverSave && $serverFolder ? $serverFolder : null}
		onserver={() => review.saveToServer()}
		onshare={canShare() ? () => review.share() : undefined}
		onstop={() => review.stop()}
		stopping={review.stopping}
	/>

	<!-- Block, not flex: WebKit counts a wrapped legend twice in a flex column's fieldset. -->
	<div class="rounded-[16px] bg-app p-3" data-testid="file-options">
		<Segmented
			name="review-format"
			label={t('settings.fileFormat')}
			hint={$exportPrefs.format === ExportFormat.Pdf
				? t('settings.perDocument')
				: t('settings.perPage')}
			options={exportFormatOptions}
			value={String($exportPrefs.format)}
			onchange={(v) => exportPrefs.set({ ...$exportPrefs, format: Number(v) as ExportFormat })}
		/>

		<PdfProperties sessionId={session.Id} />
	</div>

	<div class="flex flex-col gap-1.5" data-testid="scan-section">
		{@render heading(t('review.headingScan'), pageSummary, 'page-count')}
		<div class="mt-1 grid grid-cols-2 gap-1.5">
			{@render tile(t('review.scanned'), formatTime(session.CreatedAt))}
			{@render tile(t('review.size'), formatBytes(session.SizeBytes))}
			{#if scannerLabel}
				{@render tile(t('review.scanner'), scannerLabel, true)}
			{/if}
			{#each scanSettings as [label, value] (label)}
				{@render tile(t(label), value)}
			{/each}
		</div>
	</div>

	<div class="flex flex-col gap-0.5" data-testid="more-section">
		{@render heading(t('review.headingMore'))}
		<button
			type="button"
			class={ghost}
			onclick={() => review.scanMore()}
			disabled={!editable}
			title={editable ? undefined : busyReason}
			data-testid="scan-more-button"
		>
			<Icon name="plus" />
			{t('review.scanMorePages')}
		</button>
		<button
			type="button"
			class={ghost}
			onclick={onmerge}
			disabled={!editable || mergeable === 0 || review.mergeBusy}
			title={!editable ? busyReason : mergeable === 0 ? t('review.noMergeable') : undefined}
			data-testid="merge-button"
		>
			<Icon name="layers" />
			{t('review.addFromPastScan')}
		</button>
	</div>

	<div class="mt-auto border-t border-line pt-4 max-lg:mt-4">
		<button
			type="button"
			class="{ghost} w-full text-danger hover:bg-danger-bg"
			onclick={() => (confirmDiscard ? review.discard() : (confirmDiscard = true))}
			onblur={() => (confirmDiscard = false)}
			data-testid="discard-button"
		>
			<Icon name="trash" />
			{confirmDiscard ? t('review.reallyDiscard') : t('review.discardScan')}
		</button>
	</div>
</aside>
