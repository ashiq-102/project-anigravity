import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd
df = pd.read_csv(r"{CSV_PATH}")
by_month = df.groupby('month', sort=False)['revenue'].sum()
plt.figure()
plt.plot(by_month.index, by_month.values, marker='o')
plt.title('Total revenue by month')
plt.xlabel('Month')
plt.ylabel('Revenue')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
