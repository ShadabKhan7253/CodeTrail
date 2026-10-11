import { Pipe, PipeTransform } from '@angular/core';
import { GraphNode } from '../models/analysis.models';

@Pipe({ name: 'filterKind', standalone: true, pure: true })
export class FilterKindPipe implements PipeTransform {
  transform(nodes: GraphNode[], kind: string): GraphNode[] {
    return nodes?.filter(n => n.kind === kind) ?? [];
  }
}
