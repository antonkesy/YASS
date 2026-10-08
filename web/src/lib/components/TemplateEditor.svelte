<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		value,
		tokens,
		preview,
		presets = [],
		unknown,
		extension = '.pdf',
		onchange
	}: {
		value: string;
		/** Tokens offered as chips, without braces. */
		tokens: readonly string[];
		/** What the template expands to right now. Empty when there is no template. */
		preview: string;
		/** Whole templates worth offering, each with what it would produce. */
		presets?: readonly { template: string; example: string }[];
		/** Tokens in the template the server does not know. */
		unknown: readonly string[];
		/** What the saved file will end in, which is the export format's and not always PDF. */
		extension?: string;
		onchange: (value: string) => void;
	} = $props();

	let open = $state(false);

	let field = $state<HTMLInputElement | null>(null);
	/** Where the caret was when the field last lost focus, so a chip inserts in place. */
	let caret = $state<[number, number]>([0, 0]);

	function remember() {
		if (field) caret = [field.selectionStart ?? 0, field.selectionEnd ?? 0];
	}

	function insert(token: string) {
		const text = `{${token}}`;
		const [from, to] =
			field && document.activeElement === field
				? [field.selectionStart ?? value.length, field.selectionEnd ?? value.length]
				: caret;
		const next = value.slice(0, from) + text + value.slice(to);
		onchange(next);
		const at = from + text.length;
		caret = [at, at];
		requestAnimationFrame(() => {
			field?.focus();
			field?.setSelectionRange(at, at);
		});
	}
</script>

<div class="flex flex-col gap-2">
	<label class="field flex h-11 items-center gap-2 px-3">
		<span class="text-ink-faint"><Icon name="template" /></span>
		<span class="sr-only">File name template</span>
		<input
			bind:this={field}
			class="min-w-0 flex-1 bg-transparent font-mono text-sm font-medium outline-none"
			type="text"
			placeholder="{'{YEAR}'}-{'{MONTH}'}-{'{DAY}'}_scan"
			{value}
			oninput={(e) => onchange(e.currentTarget.value)}
			onblur={remember}
			onkeyup={remember}
			onclick={remember}
			data-testid="template-input"
		/>
		<span class="font-mono text-[13px] font-semibold text-ink-faint">{extension}</span>
	</label>

	{#if presets.length > 0}
		<div class="relative">
			<button
				type="button"
				class="flex h-7 items-center gap-1 rounded-[8px] bg-ctl px-2.5 text-xs font-semibold text-ink-2 transition-colors hover:bg-ctl-track"
				aria-expanded={open}
				onclick={() => (open = !open)}
				data-testid="template-presets"
			>
				Ready-made <span class="text-[9px] leading-none">{open ? '▴' : '▾'}</span>
			</button>

			{#if open}
				<!-- Click-catcher first, so anywhere outside closes the menu. -->
				<button
					type="button"
					class="fixed inset-0 z-34 cursor-default"
					aria-label="Close ready-made templates"
					onclick={() => (open = false)}
				></button>
				<div
					class="absolute top-[calc(100%+4px)] left-0 z-35 flex min-w-[min(420px,80vw)] animate-pop-in flex-col rounded-[14px] bg-surface p-1.5 shadow-popover"
					role="menu"
				>
					{#each presets as preset (preset.template)}
						<button
							type="button"
							role="menuitem"
							class="flex flex-col gap-0.5 rounded-[10px] px-2.5 py-2 text-left transition-colors hover:bg-accent-hover50 {preset.template ===
							value
								? 'bg-accent-50'
								: ''}"
							onclick={() => {
								onchange(preset.template);
								open = false;
							}}
						>
							<span class="font-mono text-[13px] font-bold">{preset.example}{extension}</span>
							<span class="font-mono text-[11px] text-ink-muted">{preset.template}</span>
						</button>
					{/each}
				</div>
			{/if}
		</div>
	{/if}

	<div class="flex flex-wrap gap-1.5">
		{#each tokens as token (token)}
			<button
				type="button"
				class="flex h-7 items-center rounded-[8px] bg-accent-50 px-[9px] font-mono text-xs font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100 active:scale-95"
				onclick={() => insert(token)}
			>
				{'{' + token + '}'}
			</button>
		{/each}
	</div>

	{#if unknown.length > 0}
		<p class="text-xs font-semibold text-danger" role="alert" data-testid="template-error">
			Unknown: {unknown.map((t) => `{${t}}`).join(', ')}
		</p>
	{:else if preview}
		<p
			class="rounded-[10px] bg-success-bg px-3 py-1.5 font-mono text-[13px] font-semibold text-success-ink"
			data-testid="template-preview"
		>
			{preview}{extension}
		</p>
	{/if}

	<p class="text-[11px] text-ink-faint">
		Click a tag to add it at the cursor. Scans you have not named are saved with this.
	</p>
</div>
