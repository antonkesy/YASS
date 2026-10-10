<script lang="ts">
	import { slide } from 'svelte/transition';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import { ms } from '$lib/motion';
	import {
		DOCUMENT_META_FIELDS,
		DOCUMENT_META_LABELS,
		documentMeta,
		exportPrefs,
		metaFor,
		setDocumentMeta
	} from '$lib/presets';

	let { sessionId }: { sessionId: string } = $props();
	const uid = $props.id();
	const fields = `${uid}-fields`;

	const docPrefs = $derived(metaFor($exportPrefs, $documentMeta, sessionId));
	let open = $state(false);
	const summary = $derived(
		DOCUMENT_META_FIELDS.map((f) => docPrefs[f].trim())
			.filter(Boolean)
			.join(' · ')
	);
</script>

{#if $exportPrefs.format === ExportFormat.Pdf}
	<!-- The gap above is its own padding, so it opens and shuts with it. -->
	<div
		class="flex flex-col gap-2 pt-3"
		transition:slide={{ duration: ms(220) }}
		data-testid="pdf-properties"
	>
		<button
			type="button"
			class="flex items-center gap-2 rounded-[10px] text-left"
			aria-expanded={open}
			aria-controls={fields}
			onclick={() => (open = !open)}
			data-testid="pdf-properties-toggle"
		>
			<span class="text-[13px] font-semibold text-ink-muted">{t('review.pdfProperties')}</span>
			<span class="ml-auto min-w-0 truncate text-[12px] text-ink-muted" title={summary}
				>{summary || t('review.pdfNone')}</span
			>
			<span
				class="flex-none text-ink-muted transition-transform {open ? 'rotate-90' : ''}"
				aria-hidden="true"><Icon name="chevronRight" /></span
			>
		</button>
		{#if open}
			<div
				id={fields}
				class="flex flex-col gap-2"
				transition:slide={{ duration: ms(220) }}
				data-testid="pdf-fields"
			>
				{#each DOCUMENT_META_FIELDS as field (field)}
					<label class="flex flex-col gap-1">
						<span class="text-xs text-ink-muted">{t(DOCUMENT_META_LABELS[field].label)}</span>
						<input
							class="field h-9 px-2.5 text-[13px]"
							type="text"
							autocomplete="off"
							value={docPrefs[field]}
							oninput={(e) =>
								documentMeta.set(
									setDocumentMeta(
										$documentMeta,
										sessionId,
										$exportPrefs,
										field,
										e.currentTarget.value
									)
								)}
							data-testid="pdf-{field}"
						/>
					</label>
				{/each}
			</div>
		{/if}
	</div>
{/if}
