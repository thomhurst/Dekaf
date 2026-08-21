import React from 'react';
import styles from './styles.module.css';

const formatValue = (value) =>
  new Intl.NumberFormat('en', {maximumFractionDigits: 2}).format(value);

function Bar({client, value, display, width}) {
  return (
    <div className={styles.barLine}>
      <span className={styles.client}>{client}</span>
      <span className={styles.track} aria-hidden="true">
        <span
          className={`${styles.bar} ${client === 'Dekaf' ? styles.dekaf : styles.confluent}`}
          style={{width: `${width}%`}}
        />
      </span>
      <span className={styles.value}>{display ?? formatValue(value)}</span>
    </div>
  );
}

export function ComparisonChartGrid({children}) {
  return <div className={styles.grid}>{children}</div>;
}

export default function ComparisonChart({title, description, metric, better = 'higher', items}) {
  return (
    <figure className={styles.figure}>
      <figcaption className={styles.heading}>
        <span className={styles.eyebrow}>{metric}</span>
        <strong>{title}</strong>
        {description && <span className={styles.description}>{description}</span>}
      </figcaption>

      <div className={styles.legend} aria-hidden="true">
        <span><i className={styles.dekafKey} />Dekaf</span>
        <span><i className={styles.confluentKey} />Confluent</span>
        <span className={styles.direction}>{better === 'lower' ? 'Lower is better' : 'Higher is better'}</span>
      </div>

      <div className={styles.rows}>
        {items.map((item) => {
          const maximum = Math.max(item.dekaf, item.confluent);
          const dekafWidth = maximum > 0 ? Math.max((item.dekaf / maximum) * 100, 1.5) : 0;
          const confluentWidth = maximum > 0 ? Math.max((item.confluent / maximum) * 100, 1.5) : 0;
          const dekafDisplay = item.dekafDisplay ?? formatValue(item.dekaf);
          const confluentDisplay = item.confluentDisplay ?? formatValue(item.confluent);

          return (
            <div
              className={styles.row}
              key={item.label}
              role="img"
              aria-label={`${item.label}: Dekaf ${dekafDisplay}; Confluent ${confluentDisplay}. ${better === 'lower' ? 'Lower' : 'Higher'} is better.`}
            >
              <div className={styles.label}>
                <span>{item.label}</span>
                {item.note && <small>{item.note}</small>}
              </div>
              <Bar client="Dekaf" value={item.dekaf} display={dekafDisplay} width={dekafWidth} />
              <Bar client="Confluent" value={item.confluent} display={confluentDisplay} width={confluentWidth} />
            </div>
          );
        })}
      </div>

      <p className={styles.scaleNote}>Bars are scaled within each scenario for a direct client-to-client comparison.</p>
    </figure>
  );
}
