import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd
df = pd.read_csv(r"{CSV_PATH}")
plt.figure()
plt.plot(df['month'], df['sales'], marker='o')
plt.title('Sales by month')
plt.xlabel('Month')
plt.ylabel('Sales')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
