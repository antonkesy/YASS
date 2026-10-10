<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';

	let {
		value,
		placeholder,
		extension = '.pdf',
		onrename
	}: {
		value: string | null;
		placeholder: string;
		extension?: string;
		onrename: (name: string) => void;
	} = $props();

	// Seeded by the effect below rather than from `value` directly, which would only
	// ever capture what it was on the first render.
	let draft = $state('');
	let editing = $state(false);

	$effect(() => {
		if (!editing) draft = value ?? '';
	});

	function commit() {
		editing = false;
		if (draft.trim() !== (value ?? '')) onrename(draft.trim());
	}
</script>

<div
	class="flex min-h-12 items-center gap-2 rounded-[14px] border-[1.5px] border-transparent bg-app px-3 py-1.5 transition-colors focus-within:border-accent hover:bg-accent-hover50"
	data-testid="document-name"
	data-name={value ?? ''}
>
	<span class="text-ink-faint"><Icon name="pencil" /></span>

	<label class="flex min-w-0 flex-1 items-center">
		<span class="sr-only">{t('review.documentName')}</span>
		<input
			class="min-w-0 flex-1 bg-transparent text-base font-extrabold tracking-[-0.01em] outline-none"
			type="text"
			{placeholder}
			bind:value={draft}
			onfocus={() => (editing = true)}
			onblur={commit}
			onkeydown={(e) => {
				if (e.key === 'Enter') e.currentTarget.blur();
				if (e.key === 'Escape') {
					draft = value ?? '';
					editing = false;
					e.currentTarget.blur();
				}
			}}
			data-testid="name-input"
		/>
	</label>

	<span class="font-mono text-[13px] font-semibold text-ink-faint">{extension}</span>
</div>
