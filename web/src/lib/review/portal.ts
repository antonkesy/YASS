import type { Attachment } from 'svelte/attachments';

/**
 * Moves an element to the end of <body> for as long as it lives. For things that must be
 * positioned against the viewport — a card following the pointer — and would otherwise be
 * caught by whatever transformed ancestor happened to be between them and it.
 */
export const portal: Attachment<HTMLElement> = (node) => {
	document.body.appendChild(node);
	return () => node.remove();
};
