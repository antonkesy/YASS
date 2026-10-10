import type { Attachment } from 'svelte/attachments';

/** Escapes transformed ancestors that would otherwise trap viewport-positioned (fixed) elements. */
export const portal: Attachment<HTMLElement> = (node) => {
	document.body.appendChild(node);
	return () => node.remove();
};
